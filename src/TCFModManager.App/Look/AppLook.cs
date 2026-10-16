using System.Windows;
using System.Windows.Media;
using TCFModManager.Core.Models;
using TCFModManager.Core.Services;
using Wpf.Ui.Appearance;

namespace TCFModManager.App.Look;

//
// Fork (1.4.2): Options > Look's accent colour and OLED black. Every colour of the Steam look that
// either can change is written in XAML as {look:Brush #66C0F4} or {look:Color #1A9FFF} in place of
// the literal: each is an application resource named after the colour, which Apply sets to the
// colour as drawn now (LookColors.Transform). The theme's own colour resources (SteamTheme.xaml)
// are set the same way under their own names, and WPF-UI's accent with them, so the whole window
// changes at once, without a restart.
//
public static class AppLook
{
    // Every colour asked for so far, by resource name: the colour as written, and whether the
    // resource is a Color rather than a brush.
    private static readonly Dictionary<string, (uint Written, bool IsColor)> Registered = [];

    // SteamTheme.xaml's own colours that can change, by name, as written there; and its brushes
    // drawn in one of them, by name, with that colour's name and the brush's opacity.
    private static Dictionary<string, uint>? _themeColors;
    private static Dictionary<string, (string Color, double Opacity)>? _themeBrushes;

    public static uint? Accent { get; private set; }

    public static bool Black { get; private set; }

    public static event EventHandler? Changed;

    /// <summary>Applies what the settings say. At startup, and whenever Options > Look changes.</summary>
    public static void ApplyStored()
    {
        var settings = new SettingsService().Load();
        Apply(LookColors.TryParse(settings.AccentColor, out var accent) ? accent : null, settings.OledBlack);
        CardSizes.Apply(settings.CardSize);
    }

    public static void Apply(uint? accent, bool black)
    {
        Accent = accent is { } a && (a | 0xFF000000) != LookColors.SteamBlue ? a | 0xFF000000 : null;
        Black = black;
        if (Application.Current is not { } app) return;

        foreach (var (key, (written, isColor)) in Registered) app.Resources[key] = Make(written, isColor);

        // Always set, even as written: the theme's brushes read these as DynamicResource, and
        // WPF-UI merges its own theme, with colours of the same names, after SteamTheme.xaml at
        // run time - only the application's own resources come before both.
        ReadTheme();
        foreach (var (key, written) in _themeColors!) app.Resources[key] = ToColor(Drawn(written));

        // And the brushes made from them, as brushes of their own in the same place. WPF-UI's
        // controls read these by DynamicResource; the theme's own brushes, which name a colour
        // WPF-UI also has, did not follow it reliably (Windows' accent showed through). Not the
        // brushes of SteamStyles.xaml: those follow their {look:Color} by themselves, and are
        // held by StaticResource, which a brush put here would never reach.
        foreach (var (key, (color, opacity)) in _themeBrushes!)
        {
            var brush = new SolidColorBrush(ToColor(Drawn(_themeColors[color]))) { Opacity = opacity };
            brush.Freeze();
            app.Resources[key] = brush;
        }

        // WPF-UI's own accent resources, for its controls; the shades as SteamTheme.xaml gives them.
        ApplicationAccentColorManager.Apply(
            ToColor(Drawn(LookColors.SteamBlue)), ToColor(Drawn(LookColors.SteamBlue)),
            ToColor(Drawn(LookColors.SteamBlue)), ToColor(Drawn(0xFF66C0F4)));

        AppLog.Info("Look", $"accent {(Accent is { } shown ? LookColors.ToHex(shown) : "Steam blue")}, {(black ? "OLED black" : "Steam navy")}");
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>The resource a {look:...} in XAML stands for, made on first use.</summary>
    internal static string Register(string hex, bool isColor)
    {
        if (!LookColors.TryParse(hex, out var written))
            throw new FormatException(hex); // a {look:...} written with something other than #RRGGBB or #AARRGGBB

        var key = (isColor ? LookColorPrefix : "Look.Brush.") + LookColors.ToHex(written);
        if (!Registered.ContainsKey(key))
        {
            Registered[key] = (written, isColor);
            if (Application.Current is { } app && !app.Resources.Contains(key)) app.Resources[key] = Make(written, isColor);
        }

        return key;
    }

    /// <summary>The colour as drawn now, for code that makes its own brushes.</summary>
    public static Color ColorFor(uint written) => ToColor(Drawn(written));

    public static SolidColorBrush BrushFor(uint written)
    {
        var brush = new SolidColorBrush(ColorFor(written));
        brush.Freeze();
        return brush;
    }

    // For a WebView2's background, which is a System.Drawing colour and takes no transparency.
    public static System.Drawing.Color DrawingColorFor(uint written)
    {
        var c = ColorFor(written);
        return System.Drawing.Color.FromArgb(c.R, c.G, c.B);
    }

    private static uint Drawn(uint written) => LookColors.Transform(written, Accent, Black);

    private static object Make(uint written, bool isColor) => isColor ? ToColor(Drawn(written)) : BrushFor(written);

    private static Color ToColor(uint argb) => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    //
    // Read once, from a copy of SteamTheme.xaml of its own: reading every value of the merged one
    // would make all its brushes there and then, before the accent is set, with whatever colour
    // was there then - Windows' own accent, as WPF-UI sets it at startup.
    //
    private static void ReadTheme()
    {
        if (_themeColors is not null) return;

        _themeColors = [];
        _themeBrushes = [];
        ResourceDictionary theme;
        try
        {
            theme = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/{typeof(AppLook).Assembly.GetName().Name};component/Themes/SteamTheme.xaml"),
            };
        }
        catch (Exception ex)
        {
            AppLog.Warn("Look", $"couldn't read the theme's colours: {ex.Message}");
            return;
        }

        foreach (var key in theme.Keys.OfType<string>())
        {
            if (theme[key] is not Color c) continue;
            var argb = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
            if (LookColors.RoleOf(argb) != LookColors.Role.None) _themeColors[key] = argb;
        }

        foreach (var key in theme.Keys.OfType<string>())
        {
            if (theme[key] is SolidColorBrush brush && ColorKeyOf(brush) is { } color && _themeColors.ContainsKey(color))
                _themeBrushes[key] = (color, brush.Opacity);
        }
    }

    private const string LookColorPrefix = "Look.Color.";

    // The colour a brush reads by DynamicResource: the expression WPF keeps for it is not public,
    // but its ResourceKey is.
    private static string? ColorKeyOf(SolidColorBrush brush) =>
        brush.ReadLocalValue(SolidColorBrush.ColorProperty) is { } value && value.GetType().Name == "ResourceReferenceExpression"
            ? value.GetType().GetProperty("ResourceKey")?.GetValue(value) as string
            : null;
}

//
// Fork (1.4.2): Options > Look > Card size. Subscribed items' thumbnails, gaps and card padding as
// resources the templates read, and how wide a column of cards should be (InstalledViewModel).
// HUNCH: the three sizes, set by eye from Normal, which is the size the cards have always had.
//
public static class CardSizes
{
    public static CardSize Current { get; private set; } = CardSize.Normal;

    public static event EventHandler? Changed;

    public static void Apply(CardSize size)
    {
        Current = size;
        if (Application.Current is not { } app) return;

        var (thumb, gap, pad) = size switch
        {
            CardSize.Compact => (40.0, 6.0, 8.0),
            CardSize.Roomy => (72.0, 14.0, 16.0),
            _ => (56.0, 10.0, 12.0),
        };

        app.Resources["SubscribedThumbSize"] = thumb;
        app.Resources["SubscribedCardMargin"] = new Thickness(0, 0, gap, gap);
        app.Resources["SubscribedCardPadding"] = new Thickness(pad);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    //
    // Columns for a width. Normal keeps the steps the page has always had (a column from 350px, at
    // most four); Compact fits narrower columns and more of them, Roomy wider and fewer.
    //
    public static int ColumnsFor(double width) => Current switch
    {
        CardSize.Compact => Math.Clamp((int)(width / 320), 1, 5),
        CardSize.Roomy => Math.Clamp((int)(width / 460), 1, 3),
        _ => width switch { < 700 => 1, < 1050 => 2, < 1400 => 3, _ => 4 },
    };

    public static double MinimumWidth => Current switch
    {
        CardSize.Compact => 200,
        CardSize.Roomy => 300,
        _ => 240,
    };
}
