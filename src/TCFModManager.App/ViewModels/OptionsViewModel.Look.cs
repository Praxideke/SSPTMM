using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCFModManager.App.Localization;
using TCFModManager.App.Look;
using TCFModManager.Core.Models;
using TCFModManager.Core.Services;

namespace TCFModManager.App.ViewModels;

//
// Fork (1.4.3): Options > Look's accent colour, OLED black, card size and the background
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

    //
    // The look to a file and back (Core/LookFile): the accent, OLED black, card size, the
    // background picture itself with its darkness and blur, and the groups' and mods' colours.
    //
    [ObservableProperty]
    private string? _lookFileStatus;

    private static string LookFileFilter => $"{Strings.Options_LookFileType}|*{LookFile.Extension}|{Strings.ModListFile_AllFiles}|*.*";

    [RelayCommand]
    private void SaveLookFile()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Strings.Options_LookSaveTitle,
            Filter = LookFileFilter,
            FileName = "SSPTMM" + LookFile.Extension,
            AddExtension = true,
            DefaultExt = LookFile.Extension,
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var settings = _settings.Load();
            LookFile.Capture(settings, AppServices.ModGroups.Load()).Write(dialog.FileName);
            var name = System.IO.Path.GetFileName(dialog.FileName);
            LookFileStatus = LookFile.PictureTooLarge(settings)
                ? LocalizationService.Text(Strings.Options_LookSavedNoPictureFormat, name, DownloadQueueItemViewModel.SizeLabel(LookFile.MaximumPictureBytes))
                : LocalizationService.Text(Strings.Options_LookSavedFormat, name);
            AppLog.Info("Look", $"saved to {dialog.FileName}");
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("Look", $"look file not saved: {ex.Message}");
            LookFileStatus = LocalizationService.Text(Strings.Options_LookSaveFailedFormat, ex.Message);
        }
    }

    [RelayCommand]
    private void LoadLookFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.Options_LookLoadTitle,
            Filter = LookFileFilter,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true) return;

        LookFile look;
        try
        {
            look = LookFile.Read(dialog.FileName);
        }
        catch (Exception ex) when (ex is System.IO.InvalidDataException or System.IO.IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("Look", $"{dialog.FileName} not read: {ex.Message}");
            LookFileStatus = Strings.Options_LookLoadFailed;
            return;
        }

        try
        {
            // The picture into Data\Background under a new name, as Choose picture does.
            string? picture = null;
            if (look.Background is { } bytes)
            {
                var folder = AppearanceViewModel.BackgroundDirectory;
                System.IO.Directory.CreateDirectory(folder);
                picture = System.IO.Path.Combine(folder, $"background-{DateTime.UtcNow:yyyyMMddHHmmss}{look.PictureExtension}");
                System.IO.File.WriteAllBytes(picture, bytes);
            }

            var settings = _settings.Load();
            var groups = AppServices.ModGroups.Load();
            look.ApplyTo(settings, groups, picture);
            _settings.Save(settings);
            AppServices.ModGroups.Save(groups);

            AppServices.Appearance.Refresh();
            var pictureUnreadable = picture is not null && !AppServices.Appearance.HasBackgroundImage;
            if (pictureUnreadable)
            {
                settings.BackgroundImage = picture = null;
                _settings.Save(settings);
                AppServices.Appearance.Refresh();
            }

            DeleteOldBackgrounds(keep: picture);
            AppLook.Apply(LookColors.TryParse(settings.AccentColor, out var accent) ? accent : null, settings.OledBlack);
            CardSizes.Apply(settings.CardSize);

            // This page's own controls; each saves back what is already there.
            LoadLook(settings);
            BackgroundDarkness = settings.BackgroundDarkness;
            BackgroundFileName = picture is null ? null : System.IO.Path.GetFileName(picture);
            BackgroundStatus = pictureUnreadable ? Strings.Options_BackgroundUnreadable : null;
            InstalledViewModel.Current?.LookChanged();

            LookFileStatus = LocalizationService.Text(Strings.Options_LookLoadedFormat, System.IO.Path.GetFileName(dialog.FileName));
            AppLog.Info("Look", $"loaded {dialog.FileName}");
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("Look", $"look file not applied: {ex.Message}");
            LookFileStatus = LocalizationService.Text(Strings.Options_LookSaveFailedFormat, ex.Message);
        }
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
