using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCFModManager.App.Localization;
using TCFModManager.App.Look;
using TCFModManager.Core.Models;
using TCFModManager.Core.Services;

namespace TCFModManager.App.ViewModels;

//
// Fork (1.4.2): Options > Look's accent colour, OLED black, card size and the background
// picture's blur. Each takes effect at once (Look/AppLook, Look/CardSizes, AppearanceViewModel).
//
public partial class OptionsViewModel
{
    public IReadOnlyList<AccentSwatch> AccentSwatches { get; } =
    [
        new(nameof(Strings.Options_AccentSteamBlue), LookColors.SteamBlue),
        new(nameof(Strings.Options_AccentGreen), 0xFF5BA32B),
        new(nameof(Strings.Options_AccentOrange), 0xFFE8862A),
        new(nameof(Strings.Options_AccentPurple), 0xFF8E63E0),
        new(nameof(Strings.Options_AccentRed), 0xFFD9443A),
        new(nameof(Strings.Options_AccentPink), 0xFFE0559A),
    ];

    // The accent as typed: six hex digits, with or without the #.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccentInputInvalid))]
    private string _accentInput = string.Empty;

    public bool AccentInputInvalid => !TryReadAccent(AccentInput, out _);

    [ObservableProperty]
    private bool _oledBlack;

    private static readonly CardSizeItem[] CardSizeChoices =
    [
        new(nameof(Strings.Options_CardSizeCompact), CardSize.Compact),
        new(nameof(Strings.Options_CardSizeNormal), CardSize.Normal),
        new(nameof(Strings.Options_CardSizeRoomy), CardSize.Roomy),
    ];

    public IReadOnlyList<CardSizeItem> CardSizeOptions => CardSizeChoices;

    [ObservableProperty]
    private CardSizeItem _selectedCardSize = CardSizeChoices[1];

    // In pixels, 0 to AppearanceViewModel.MaximumBlur.
    [ObservableProperty]
    private double _backgroundBlur;

    public double MaximumBlur => AppearanceViewModel.MaximumBlur;

    // From the constructor, before _loaded is set, so nothing here is saved back.
    private void LoadLook(AppSettings settings)
    {
        var accent = LookColors.TryParse(settings.AccentColor, out var stored) ? stored | 0xFF000000 : LookColors.SteamBlue;
        AccentInput = LookColors.ToHex(accent);
        MarkSwatch(accent);
        OledBlack = settings.OledBlack;
        SelectedCardSize = CardSizeChoices.FirstOrDefault(o => o.Value == settings.CardSize) ?? CardSizeChoices[1];
        BackgroundBlur = Math.Clamp(settings.BackgroundBlur, 0, AppearanceViewModel.MaximumBlur);
    }

    [RelayCommand]
    private void PickAccent(AccentSwatch swatch) => AccentInput = LookColors.ToHex(swatch.Value);

    partial void OnAccentInputChanged(string value)
    {
        if (!_loaded || !TryReadAccent(value, out var accent)) return;

        MarkSwatch(accent);
        SaveLook(s => s.AccentColor = accent == LookColors.SteamBlue ? null : LookColors.ToHex(accent));
    }

    partial void OnOledBlackChanged(bool value) => SaveLook(s => s.OledBlack = value);

    partial void OnSelectedCardSizeChanged(CardSizeItem value)
    {
        if (!_loaded) return;

        var settings = _settings.Load();
        settings.CardSize = value.Value;
        _settings.Save(settings);
        CardSizes.Apply(value.Value);
        AppLog.Info("Look", $"card size {value.Value}");
    }

    partial void OnBackgroundBlurChanged(double value) =>
        SaveAppearance(s => s.BackgroundBlur = Math.Clamp(value, 0, AppearanceViewModel.MaximumBlur));

    private void SaveLook(Action<AppSettings> change)
    {
        if (!_loaded) return;

        var settings = _settings.Load();
        change(settings);
        _settings.Save(settings);
        AppLook.Apply(LookColors.TryParse(settings.AccentColor, out var accent) ? accent : null, settings.OledBlack);
    }

    private void MarkSwatch(uint accent)
    {
        foreach (var swatch in AccentSwatches) swatch.IsSelected = swatch.Value == accent;
    }

    // Opaque only: a see-through accent would let the page through every button.
    private static bool TryReadAccent(string? text, out uint accent)
    {
        accent = 0;
        return text?.Trim().TrimStart('#').Length == 6 && LookColors.TryParse(text, out accent);
    }
}

// One of Options > Look's accent colours, drawn as itself and named for a screen reader.
public sealed partial class AccentSwatch : LocalizedViewModel
{
    private readonly string _key;

    public AccentSwatch(string key, uint value)
    {
        _key = key;
        Value = value;
        var fill = new SolidColorBrush(Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value));
        fill.Freeze();
        Fill = fill;
    }

    public uint Value { get; }

    public Brush Fill { get; }

    public string Label => LocalizationService.Get(_key);

    [ObservableProperty]
    private bool _isSelected;

    public override string ToString() => Label;
}

// One entry in Options > Look's Card size dropdown.
public sealed class CardSizeItem(string key, CardSize value) : LocalizedViewModel
{
    public CardSize Value { get; } = value;

    public string Label => LocalizationService.Get(key);

    public override string ToString() => Label;
}
