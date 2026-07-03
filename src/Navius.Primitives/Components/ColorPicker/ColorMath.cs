using System.Globalization;

namespace Navius.Primitives.Components.ColorPicker;

/// <summary>
/// Self-contained color conversions + parse/format for the ColorPicker. The
/// authoritative model is HSVA (hue 0..360, saturation/value/alpha 0..1); RGB is
/// derived. Parsing accepts hex, rgb()/rgba() and hsl()/hsla(); formatting emits
/// whichever the picker's <c>Format</c> asks for. No dependencies, invariant
/// culture throughout.
/// </summary>
internal static class ColorMath
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

    public static double Wrap360(double h)
    {
        h %= 360;
        return h < 0 ? h + 360 : h;
    }

    public static (int R, int G, int B) HsvToRgb(double h, double s, double v)
    {
        h = Wrap360(h);
        s = Clamp01(s);
        v = Clamp01(v);

        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60d) % 2 - 1));
        var m = v - c;

        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return (
            (int)Math.Round((r + m) * 255),
            (int)Math.Round((g + m) * 255),
            (int)Math.Round((b + m) * 255));
    }

    public static (double H, double S, double V) RgbToHsv(int r, int g, int b)
    {
        var rn = r / 255d;
        var gn = g / 255d;
        var bn = b / 255d;

        var max = Math.Max(rn, Math.Max(gn, bn));
        var min = Math.Min(rn, Math.Min(gn, bn));
        var d = max - min;

        double h;
        if (d == 0) h = 0;
        else if (max == rn) h = 60 * (((gn - bn) / d) % 6);
        else if (max == gn) h = 60 * ((bn - rn) / d + 2);
        else h = 60 * ((rn - gn) / d + 4);

        var s = max == 0 ? 0 : d / max;
        return (Wrap360(h), s, max);
    }

    // HSV -> HSL (for the hsl/hsla output formats).
    public static (double H, double S, double L) HsvToHsl(double h, double s, double v)
    {
        var l = v * (1 - s / 2);
        double sl;
        if (l == 0 || l == 1) sl = 0;
        else sl = (v - l) / Math.Min(l, 1 - l);
        return (Wrap360(h), Clamp01(sl), Clamp01(l));
    }

    private static (int R, int G, int B) HslToRgb(double h, double s, double l)
    {
        h = Wrap360(h);
        s = Clamp01(s);
        l = Clamp01(l);

        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs((h / 60d) % 2 - 1));
        var m = l - c / 2;

        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return (
            (int)Math.Round((r + m) * 255),
            (int)Math.Round((g + m) * 255),
            (int)Math.Round((b + m) * 255));
    }

    /// <summary>Parse hex / rgb(a) / hsl(a) into HSVA. Returns false on unparseable input.</summary>
    public static bool TryParse(string? input, out double h, out double s, out double v, out double a)
    {
        h = 0; s = 0; v = 0; a = 1;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var text = input.Trim();

        if (text.StartsWith("#", StringComparison.Ordinal) || IsBareHex(text))
        {
            return TryParseHex(text, out h, out s, out v, out a);
        }

        if (text.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var parts = ExtractNumbers(text);
            if (parts.Count < 3) return false;
            var r = (int)Math.Round(parts[0]);
            var g = (int)Math.Round(parts[1]);
            var b = (int)Math.Round(parts[2]);
            (h, s, v) = RgbToHsv(Clamp255(r), Clamp255(g), Clamp255(b));
            a = parts.Count >= 4 ? Clamp01(parts[3]) : 1;
            return true;
        }

        if (text.StartsWith("hsl", StringComparison.OrdinalIgnoreCase))
        {
            var parts = ExtractNumbers(text);
            if (parts.Count < 3) return false;
            var (r, g, b) = HslToRgb(parts[0], parts[1] / 100d, parts[2] / 100d);
            (h, s, v) = RgbToHsv(r, g, b);
            a = parts.Count >= 4 ? Clamp01(parts[3]) : 1;
            return true;
        }

        return false;
    }

    private static bool IsBareHex(string text)
        => (text.Length == 3 || text.Length == 4 || text.Length == 6 || text.Length == 8)
           && text.All(Uri.IsHexDigit);

    private static bool TryParseHex(string text, out double h, out double s, out double v, out double a)
    {
        h = 0; s = 0; v = 0; a = 1;
        var hex = text.StartsWith("#", StringComparison.Ordinal) ? text[1..] : text;

        if (!hex.All(Uri.IsHexDigit))
        {
            return false;
        }

        int r, g, b;
        var al = 255;

        switch (hex.Length)
        {
            case 3:
                r = HexPair(hex[0], hex[0]);
                g = HexPair(hex[1], hex[1]);
                b = HexPair(hex[2], hex[2]);
                break;
            case 4:
                r = HexPair(hex[0], hex[0]);
                g = HexPair(hex[1], hex[1]);
                b = HexPair(hex[2], hex[2]);
                al = HexPair(hex[3], hex[3]);
                break;
            case 6:
                r = HexPair(hex[0], hex[1]);
                g = HexPair(hex[2], hex[3]);
                b = HexPair(hex[4], hex[5]);
                break;
            case 8:
                r = HexPair(hex[0], hex[1]);
                g = HexPair(hex[2], hex[3]);
                b = HexPair(hex[4], hex[5]);
                al = HexPair(hex[6], hex[7]);
                break;
            default:
                return false;
        }

        (h, s, v) = RgbToHsv(r, g, b);
        a = al / 255d;
        return true;
    }

    private static int HexPair(char hi, char lo)
        => Convert.ToInt32($"{hi}{lo}", 16);

    private static List<double> ExtractNumbers(string text)
    {
        var numbers = new List<double>();
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            var isNumeric = char.IsDigit(ch) || ch == '.' || ch == '-' || ch == '+';
            if (isNumeric)
            {
                if (start < 0) start = i;
            }
            else if (start >= 0)
            {
                if (double.TryParse(text[start..i], NumberStyles.Float, Inv, out var n)) numbers.Add(n);
                start = -1;
            }
        }

        if (start >= 0 && double.TryParse(text[start..], NumberStyles.Float, Inv, out var last))
        {
            numbers.Add(last);
        }

        return numbers;
    }

    private static int Clamp255(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    /// <summary>Format HSVA as the requested <paramref name="format"/> string.</summary>
    public static string Format(string format, double h, double s, double v, double a)
    {
        var (r, g, b) = HsvToRgb(h, s, v);
        switch ((format ?? "hex").ToLowerInvariant())
        {
            case "rgb":
                return $"rgb({r}, {g}, {b})";
            case "rgba":
                return $"rgba({r}, {g}, {b}, {Round(a)})";
            case "hsl":
            {
                var (hh, sl, ll) = HsvToHsl(h, s, v);
                return $"hsl({(int)Math.Round(hh)}, {(int)Math.Round(sl * 100)}%, {(int)Math.Round(ll * 100)}%)";
            }
            case "hsla":
            {
                var (hh, sl, ll) = HsvToHsl(h, s, v);
                return $"hsla({(int)Math.Round(hh)}, {(int)Math.Round(sl * 100)}%, {(int)Math.Round(ll * 100)}%, {Round(a)})";
            }
            default:
                return a < 1
                    ? $"#{r:X2}{g:X2}{b:X2}{(int)Math.Round(a * 255):X2}"
                    : $"#{r:X2}{g:X2}{b:X2}";
        }
    }

    private static string Round(double a) => Math.Round(a, 2).ToString(Inv);

    /// <summary>An <c>rgba()</c> string for a CSS background/preview.</summary>
    public static string Rgba(double h, double s, double v, double a)
    {
        var (r, g, b) = HsvToRgb(h, s, v);
        return $"rgba({r}, {g}, {b}, {Round(a)})";
    }
}
