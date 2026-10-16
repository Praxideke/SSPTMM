using System.Globalization;

namespace TCFModManager.Core.Services;

//
// Fork (1.4.2): the colour arithmetic behind Options > Look's accent colour and OLED black. Every
// colour the Steam look is drawn with goes through Transform: Steam's blues follow the accent,
// the dark greys and navies of the pages go to black. Colours are 0xAARRGGBB.
//
public static class LookColors
{
    /// <summary>Steam's new-UI blue, the accent the look is drawn in.</summary>
    public const uint SteamBlue = 0xFF1A9FFF;

    public enum Role
    {
        None,

        // One of Steam's blues: buttons, the current tab's bar, links, ticks.
        Accent,

        // A page, bar, card or dialog surface.
        Dark,
    }

    //
    // Blue enough to be the accent: a hue between cyan and azure that is clearly coloured. The
    // navy of the pages (#1B2838, saturation 0.35) is just under the line, so it counts as a
    // surface and stays put when only the accent changes.
    //
    // A surface is near black, or one of Steam's navies: the same blue hue, only tinted, and dark
    // (the cards' #273B52). HUNCH: the lines, set from the colours the look uses; a grey such as
    // the buttons' #3D4450 is under the tint and stays as it is.
    //
    public static Role RoleOf(uint argb)
    {
        var (r, g, b) = Channels(argb);
        var (h, s, l) = ToHsl(r, g, b);
        var blue = h is >= 190 and <= 225;
        if (blue && s >= 0.4 && l is >= 0.12 and <= 0.92) return Role.Accent;
        if ((argb >> 24) == 0 || (argb & 0xFFFFFF) == 0) return Role.None;
        if (Math.Max(r, Math.Max(g, b)) <= 0x45) return Role.Dark;
        if (blue && s >= 0.2 && l <= 0.3) return Role.Dark;
        return Role.None;
    }

    /// <summary>The colour as drawn with this accent (null for Steam blue) and, when black is on,
    /// OLED black.</summary>
    public static uint Transform(uint argb, uint? accent, bool black) => RoleOf(argb) switch
    {
        Role.Accent when accent is { } a && (a | 0xFF000000) != SteamBlue => ShiftAccent(argb, a),
        Role.Dark when black => Blacken(argb),
        _ => argb,
    };

    //
    // Moves one of Steam's blues to where the accent sits: the hue turned by as much as the accent
    // is turned from Steam blue, the saturation scaled the same way, and the lightness moved by the
    // accent's difference - less towards white and black, so a pale text blue stays pale and a dark
    // edge stays dark.
    //
    public static uint ShiftAccent(uint argb, uint accent)
    {
        var (h0, s0, l0) = ToHsl(Channels(SteamBlue));
        var (ha, sa, la) = ToHsl(Channels(accent));
        var (h, s, l) = ToHsl(Channels(argb));

        var nh = (h + (ha - h0) + 360) % 360;
        var ns = Math.Clamp(s * sa / s0, 0, 1);
        var nl = Math.Clamp(l + (la - l0) * (1 - Math.Abs(2 * l - 1)) / (1 - Math.Abs(2 * l0 - 1)), 0, 1);

        var (r, g, b) = FromHsl(nh, ns, nl);
        return (argb & 0xFF000000) | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    //
    // OLED black. The page backgrounds themselves go to black; every other surface keeps a little
    // of its light (HUNCH: 45%), so a card or dialog still stands off the page behind it.
    //
    public static readonly IReadOnlySet<uint> PageBackgrounds = new HashSet<uint>
    {
        0xFF262B34, // the Workshop pages
        0xFF1B2838, // the item page and Steam's navy
        0xFF171D25, // the header
        0xFF171A21, // the downloads bar
    };

    public const double SurfaceLight = 0.45;

    public static uint Blacken(uint argb)
    {
        if (PageBackgrounds.Contains(argb | 0xFF000000)) return argb & 0xFF000000;

        var (r, g, b) = Channels(argb);
        byte Dim(byte c) => (byte)Math.Round(c * SurfaceLight);
        return (argb & 0xFF000000) | ((uint)Dim(r) << 16) | ((uint)Dim(g) << 8) | Dim(b);
    }

    /// <summary>#RGB is not taken: XAML and the settings both write six or eight digits.</summary>
    public static bool TryParse(string? text, out uint argb)
    {
        argb = 0;
        var hex = text?.Trim().TrimStart('#');
        if (hex is not { Length: 6 or 8 } || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return false;

        argb = hex.Length == 6 ? 0xFF000000 | value : value;
        return true;
    }

    public static string ToHex(uint argb) => (argb >> 24) == 0xFF
        ? "#" + (argb & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture)
        : "#" + argb.ToString("X8", CultureInfo.InvariantCulture);

    private static (byte R, byte G, byte B) Channels(uint argb) => ((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    private static (double H, double S, double L) ToHsl((byte R, byte G, byte B) c) => ToHsl(c.R, c.G, c.B);

    private static (double H, double S, double L) ToHsl(byte rb, byte gb, byte bb)
    {
        double r = rb / 255.0, g = gb / 255.0, b = bb / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        if (max == min) return (0, 0, l);

        var d = max - min;
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h;
        if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g) h = (b - r) / d + 2;
        else h = (r - g) / d + 4;
        return (h * 60, s, l);
    }

    private static (byte R, byte G, byte B) FromHsl(double h, double s, double l)
    {
        if (s == 0)
        {
            var v = (byte)Math.Round(l * 255);
            return (v, v, v);
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        double Hue(double t)
        {
            t = (t % 1 + 1) % 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        var hh = h / 360;
        return ((byte)Math.Round(Hue(hh + 1.0 / 3) * 255), (byte)Math.Round(Hue(hh) * 255), (byte)Math.Round(Hue(hh - 1.0 / 3) * 255));
    }
}
