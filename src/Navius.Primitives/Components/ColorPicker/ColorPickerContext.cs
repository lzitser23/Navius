namespace Navius.Primitives.Components.ColorPicker;

/// <summary>
/// Shared state for a ColorPicker. The authoritative color lives here as HSVA so
/// the 2D area (saturation/value), hue slider and alpha slider can each drive one
/// axis without a lossy round-trip through the projected string. Every mutating
/// method funnels through the root-supplied <c>commit</c> callback, which projects
/// HSVA to the <see cref="Format"/> string and raises the value change. Parts read
/// the projections (thumb offsets, gradients, preview color) and subscribe to
/// <see cref="Changed"/> to repaint.
/// </summary>
public sealed class ColorPickerContext
{
    private readonly Func<Task> _commit;

    public ColorPickerContext(Func<Task> commit)
    {
        _commit = commit;
    }

    public double H { get; private set; }

    public double S { get; private set; }

    public double V { get; private set; }

    public double A { get; private set; } = 1;

    public string Format { get; private set; } = "hex";

    public bool AlphaEnabled { get; private set; }

    public bool Disabled { get; private set; }

    public bool ReadOnly { get; private set; }

    public event Func<Task>? Changed;

    public void Configure(string format, bool alphaEnabled, bool disabled, bool readOnly)
    {
        Format = string.IsNullOrEmpty(format) ? "hex" : format;
        AlphaEnabled = alphaEnabled;
        Disabled = disabled;
        ReadOnly = readOnly;
    }

    /// <summary>Set HSVA without projecting/notifying (used to seed from a controlled value).</summary>
    internal void SetHsvaInternal(double h, double s, double v, double a)
    {
        H = ColorMath.Wrap360(h);
        S = ColorMath.Clamp01(s);
        V = ColorMath.Clamp01(v);
        A = ColorMath.Clamp01(a);
    }

    public async Task NotifyChangedAsync()
    {
        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    public async Task SetSaturationValueAsync(double s, double v)
    {
        if (Disabled || ReadOnly) return;
        S = ColorMath.Clamp01(s);
        V = ColorMath.Clamp01(v);
        await _commit();
    }

    public async Task SetHueAsync(double h)
    {
        if (Disabled || ReadOnly) return;
        H = ColorMath.Wrap360(h);
        await _commit();
    }

    public async Task SetAlphaAsync(double a)
    {
        if (Disabled || ReadOnly) return;
        A = ColorMath.Clamp01(a);
        await _commit();
    }

    /// <summary>Parse an arbitrary color string (hex/rgb/hsl) into the model. Returns false if unparseable.</summary>
    public async Task<bool> SetFromStringAsync(string value)
    {
        if (Disabled || ReadOnly) return false;
        if (!ColorMath.TryParse(value, out var h, out var s, out var v, out var a))
        {
            return false;
        }

        H = h;
        S = s;
        V = v;
        if (AlphaEnabled)
        {
            A = a;
        }

        await _commit();
        return true;
    }

    // Projections -----------------------------------------------------------

    public (int R, int G, int B) Rgb => ColorMath.HsvToRgb(H, S, V);

    /// <summary>The color projected as the current <see cref="Format"/> string.</summary>
    public string Projected => ColorMath.Format(Format, H, S, V, AlphaEnabled ? A : 1);

    /// <summary>Always the hex form (for the hex field default).</summary>
    public string HexValue => ColorMath.Format("hex", H, S, V, AlphaEnabled ? A : 1);

    /// <summary>An rgba() CSS string for previews/swatches (honours alpha).</summary>
    public string CssColor => ColorMath.Rgba(H, S, V, AlphaEnabled ? A : 1);

    /// <summary>The pure hue as an rgb() string (the area's base fill).</summary>
    public string HueCss
    {
        get
        {
            var (r, g, b) = ColorMath.HsvToRgb(H, 1, 1);
            return $"rgb({r}, {g}, {b})";
        }
    }

    public string AreaThumbLeft => Pct(S);

    public string AreaThumbTop => Pct(1 - V);

    public string HueThumbLeft => Pct(H / 360d);

    public string AlphaThumbLeft => Pct(A);

    /// <summary>A short human color description for aria-valuetext.</summary>
    public string Description
    {
        get
        {
            var (hh, sl, ll) = ColorMath.HsvToHsl(H, S, V);
            var alpha = AlphaEnabled && A < 1 ? $", {(int)Math.Round(A * 100)}% opacity" : string.Empty;
            return $"hue {(int)Math.Round(hh)} degrees, saturation {(int)Math.Round(sl * 100)}%, lightness {(int)Math.Round(ll * 100)}%{alpha}";
        }
    }

    private static string Pct(double f)
        => (ColorMath.Clamp01(f) * 100).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "%";
}
