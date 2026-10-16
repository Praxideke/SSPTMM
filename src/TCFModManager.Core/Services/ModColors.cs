namespace TCFModManager.Core.Services;

//
// Fork (1.4.2): the colours a mod or a group can be given on Subscribed items (right-click >
// Colour): ten of a palette, or any colour as six hex digits. Stored as "#RRGGBB".
//
// HUNCH: the ten, chosen by eye against Steam's navy (#1B2838) and the Workshop grey (#262B34),
// as far apart in hue and lightness as ten can be. Some will still look alike to someone who is
// colour blind, so a colour never says anything alone: the group's name and the tooltip say it too.
//
public static class ModColors
{
    public sealed record Swatch(string Key, uint Argb)
    {
        public string Hex => LookColors.ToHex(Argb);
    }

    public static IReadOnlyList<Swatch> Palette { get; } =
    [
        new("red", 0xFFE5534B),
        new("orange", 0xFFF0883E),
        new("yellow", 0xFFD9B53A),
        new("green", 0xFF57AB5A),
        new("teal", 0xFF39C5BB),
        new("blue", 0xFF4493F8),
        new("purple", 0xFFA371F7),
        new("pink", 0xFFE85AAD),
        new("brown", 0xFFA8754A),
        new("grey", 0xFF8B949E),
    ];

    // What "color:none" matches in the search box.
    public const string NoneKey = "none";

    /// <summary>"#RRGGBB" in capitals, or null when the text is not six (or eight, opaque) hex digits.</summary>
    public static string? Normalize(string? text) =>
        LookColors.TryParse(text, out var argb) && (argb >> 24) == 0xFF ? LookColors.ToHex(argb) : null;

    /// <summary>The palette's colour this is, or null for a colour of your own (or none).</summary>
    public static Swatch? SwatchOf(string? hex) =>
        Normalize(hex) is { } normal ? Palette.FirstOrDefault(s => s.Hex == normal) : null;

    //
    // Whether a mod drawn in this colour (null for none) is what "color:<query>" asks for: a
    // palette colour by its key or by its name as shown (nameOf, in the app's language), "none",
    // or a colour as hex. Case aside.
    //
    public static bool Matches(string? hex, string query, Func<Swatch, string> nameOf)
    {
        query = query.Trim();
        if (query.Length == 0) return true;
        if (string.Equals(query, NoneKey, StringComparison.OrdinalIgnoreCase)) return Normalize(hex) is null;
        if (Normalize(hex) is not { } normal) return false;
        if (Normalize(query) is { } asked) return asked == normal;

        return SwatchOf(normal) is { } swatch
            && (string.Equals(swatch.Key, query, StringComparison.OrdinalIgnoreCase)
                || string.Equals(nameOf(swatch), query, StringComparison.CurrentCultureIgnoreCase));
    }
}
