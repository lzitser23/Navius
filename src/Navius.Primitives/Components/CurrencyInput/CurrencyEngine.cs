using System.Globalization;
using System.Text;

namespace Navius.Primitives.Components.CurrencyInput;

/// <summary>
/// The pure currency parse/format core for <c>NaviusCurrencyInput</c>. Internal truth is a
/// <see cref="decimal"/>; the display string is derived from <see cref="NumberFormatInfo"/>
/// parts (we reimplement the <c>Intl.NumberFormat</c> semantics with
/// <see cref="System.Globalization"/> so there is no JS locale round-trip). Digits are the
/// editable characters; the symbol, grouping separators and decimal point are fixed tokens,
/// so the caret is kept stable by counting digits (see the caret helpers).
///
/// Simplifications vs a full ICU formatter (documented deviations): grouping is fixed at
/// three digits (<see cref="NumberFormatInfo.CurrencyGroupSizes"/> is not walked) and a
/// negative value is rendered as <c>NegativeSign + positive</c> rather than following the
/// culture's parenthesised <see cref="NumberFormatInfo.CurrencyNegativePattern"/>.
/// </summary>
internal static class CurrencyEngine
{
    /// <summary>Parse a partly-typed display string into (sign, integer digits, fraction digits, sawSeparator).</summary>
    public static (bool Negative, string IntDigits, string FracDigits, bool HadSeparator) Parse(
        string s, NumberFormatInfo nfi, bool allowNegative, int maxFrac)
    {
        var negative = allowNegative && s.Contains(nfi.NegativeSign, StringComparison.Ordinal);

        var sep = nfi.CurrencyDecimalSeparator;
        var sepIndex = maxFrac > 0 ? s.IndexOf(sep, StringComparison.Ordinal) : -1;

        string intRaw, fracRaw;
        var hadSep = false;
        if (sepIndex >= 0)
        {
            hadSep = true;
            intRaw = s.Substring(0, sepIndex);
            fracRaw = s.Substring(sepIndex + sep.Length);
        }
        else
        {
            intRaw = s;
            fracRaw = "";
        }

        var intDigits = StripLeadingZeros(DigitsOnly(intRaw));
        var fracDigits = DigitsOnly(fracRaw);
        if (fracDigits.Length > maxFrac)
        {
            fracDigits = fracDigits.Substring(0, maxFrac);
        }

        return (negative, intDigits, fracDigits, hadSep && maxFrac > 0);
    }

    /// <summary>Build the decimal truth from parsed parts (null when there is no digit at all).</summary>
    public static decimal? ToDecimal(bool negative, string intDigits, string fracDigits)
    {
        if (intDigits.Length == 0 && fracDigits.Length == 0)
        {
            return null;
        }

        var i = intDigits.Length == 0 ? "0" : intDigits;
        var num = fracDigits.Length > 0 ? $"{i}.{fracDigits}" : i;
        if (!decimal.TryParse(num, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return negative ? -value : value;
    }

    /// <summary>Format while the user is mid-edit: group the integer, keep fraction as typed (and a lone trailing separator).</summary>
    public static string FormatEditing(
        bool negative, string intDigits, string fracDigits, bool hadSep, NumberFormatInfo nfi, bool showSymbol)
    {
        if (intDigits.Length == 0 && fracDigits.Length == 0 && !hadSep)
        {
            return "";
        }

        var grouped = Group(intDigits.Length == 0 ? "0" : intDigits, nfi.CurrencyGroupSeparator);
        var core = hadSep ? grouped + nfi.CurrencyDecimalSeparator + fracDigits : grouped;
        var symbolized = Symbolize(core, nfi, showSymbol);
        return negative ? nfi.NegativeSign + symbolized : symbolized;
    }

    /// <summary>Format a committed value on blur: pad/trim fraction to [minFrac, maxFrac] and group.</summary>
    public static string FormatCommitted(
        decimal value, NumberFormatInfo nfi, int minFrac, int maxFrac, bool showSymbol)
    {
        var negative = value < 0;
        var abs = Math.Abs(value);
        var fixedStr = abs.ToString("F" + maxFrac, CultureInfo.InvariantCulture);

        var dot = fixedStr.IndexOf('.');
        string intDigits, fracDigits;
        if (dot >= 0)
        {
            intDigits = fixedStr.Substring(0, dot);
            fracDigits = fixedStr.Substring(dot + 1);
        }
        else
        {
            intDigits = fixedStr;
            fracDigits = "";
        }

        while (fracDigits.Length > minFrac && fracDigits.EndsWith("0", StringComparison.Ordinal))
        {
            fracDigits = fracDigits.Substring(0, fracDigits.Length - 1);
        }

        var grouped = Group(intDigits, nfi.CurrencyGroupSeparator);
        var core = fracDigits.Length > 0 ? grouped + nfi.CurrencyDecimalSeparator + fracDigits : grouped;
        var symbolized = Symbolize(core, nfi, showSymbol);
        return negative ? nfi.NegativeSign + symbolized : symbolized;
    }

    /// <summary>Count the digits left of <paramref name="caret"/> (the caret anchor is a digit count).</summary>
    public static int CountDigitsBefore(string s, int caret)
    {
        var n = 0;
        for (var i = 0; i < caret && i < s.Length; i++)
        {
            if (char.IsDigit(s[i]))
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>Re-land the caret after the <paramref name="digitTarget"/>-th digit in the freshly formatted string.</summary>
    public static int CaretForDigits(string formatted, int digitTarget)
    {
        if (digitTarget <= 0)
        {
            for (var i = 0; i < formatted.Length; i++)
            {
                if (char.IsDigit(formatted[i]))
                {
                    return i;
                }
            }

            return formatted.Length;
        }

        var seen = 0;
        for (var i = 0; i < formatted.Length; i++)
        {
            if (!char.IsDigit(formatted[i]))
            {
                continue;
            }

            seen++;
            if (seen == digitTarget)
            {
                return i + 1;
            }
        }

        return formatted.Length;
    }

    /// <summary>Resolve a currency symbol for an ISO 4217 code (common ones mapped; otherwise the code itself).</summary>
    public static string SymbolFor(string isoCode) => isoCode.ToUpperInvariant() switch
    {
        "USD" or "CAD" or "AUD" or "MXN" or "NZD" or "SGD" or "HKD" => "$",
        "EUR" => "€",
        "GBP" => "£",
        "JPY" or "CNY" => "¥",
        "INR" => "₹",
        "KRW" => "₩",
        "ILS" => "₪",
        "CHF" => "CHF",
        _ => isoCode,
    };

    private static string Symbolize(string core, NumberFormatInfo nfi, bool showSymbol)
    {
        if (!showSymbol)
        {
            return core;
        }

        var s = nfi.CurrencySymbol;
        return nfi.CurrencyPositivePattern switch
        {
            0 => s + core,
            1 => core + s,
            2 => s + " " + core,
            3 => core + " " + s,
            _ => s + core,
        };
    }

    private static string Group(string digits, string separator)
    {
        if (digits.Length <= 3)
        {
            return digits;
        }

        var sb = new StringBuilder();
        var first = digits.Length % 3;
        if (first == 0)
        {
            first = 3;
        }

        sb.Append(digits, 0, first);
        for (var i = first; i < digits.Length; i += 3)
        {
            sb.Append(separator);
            sb.Append(digits, i, 3);
        }

        return sb.ToString();
    }

    private static string DigitsOnly(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsDigit(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string StripLeadingZeros(string digits)
    {
        if (digits.Length == 0)
        {
            return "";
        }

        var trimmed = digits.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }
}
