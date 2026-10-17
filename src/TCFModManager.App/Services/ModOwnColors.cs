using System.Windows.Media;
using TCFModManager.App.Localization;
using TCFModManager.App.ViewModels;
using TCFModManager.Core.Services;

namespace TCFModManager.App.Services;

//
// Fork (1.4.3): a colour of your own for an installed mod (right-click > Colour), drawn down its
// card's edge in place of its group's. Kept in AppSettings.ModColors under the same key as a name
// you gave it (ModNicknames), so it follows a listed mod through updates.
//
public static class ModOwnColors
{
    public static void Apply(IEnumerable<InstalledModCardViewModel> cards)
    {
        var colors = new SettingsService().Load().ModColors;
        foreach (var card in cards) card.OwnColor = ModColors.Normalize(colors.GetValueOrDefault(ModNicknames.KeyFor(card)));
    }

    /// <summary>Gives every one of these mods the colour, or (null) takes theirs away.</summary>
    public static void Set(IReadOnlyList<InstalledModCardViewModel> cards, string? color)
    {
        color = ModColors.Normalize(color);
        var service = new SettingsService();
        var settings = service.Load();
        foreach (var card in cards)
        {
            if (color is null) settings.ModColors.Remove(ModNicknames.KeyFor(card));
            else settings.ModColors[ModNicknames.KeyFor(card)] = color;
            card.OwnColor = color;
        }

        service.Save(settings);
        AppLog.Info("Installed", $"{cards.Count} mod(s): colour {color ?? "removed"}");
    }

    /// <summary>The colour's name as the menus show it: the palette's name, or its hex.</summary>
    public static string NameOf(string hex) =>
        ModColors.SwatchOf(hex) is { } swatch ? NameOf(swatch) : hex;

    public static string NameOf(ModColors.Swatch swatch) => swatch.Key switch
    {
        "red" => Strings.Color_Red,
        "orange" => Strings.Color_Orange,
        "yellow" => Strings.Color_Yellow,
        "green" => Strings.Color_Green,
        "teal" => Strings.Color_Teal,
        "blue" => Strings.Color_Blue,
        "purple" => Strings.Color_Purple,
        "pink" => Strings.Color_Pink,
        "brown" => Strings.Color_Brown,
        "grey" => Strings.Color_Grey,
        _ => swatch.Hex,
    };

    private static readonly Dictionary<string, SolidColorBrush> Brushes = [];

    /// <summary>A frozen brush of the colour, made once.</summary>
    public static SolidColorBrush? BrushOf(string? hex)
    {
        if (ModColors.Normalize(hex) is not { } normal || !LookColors.TryParse(normal, out var argb)) return null;
        if (Brushes.TryGetValue(normal, out var brush)) return brush;

        brush = new SolidColorBrush(Color.FromRgb((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        brush.Freeze();
        Brushes[normal] = brush;
        return brush;
    }
}
