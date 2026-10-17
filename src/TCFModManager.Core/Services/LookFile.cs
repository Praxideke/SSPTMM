using System.Text.Json;
using System.Text.Json.Serialization;
using TCFModManager.Core.Models;

namespace TCFModManager.Core.Services;

//
// Fork (1.4.3): Options > Look saved to a file and read back - on another PC, or after trying
// something else. The accent, OLED black, card size, the background picture itself (so the file
// is all that has to be moved) with its darkness and blur, the groups' colours by the groups'
// names, and the mods' own colours. Nothing else of the settings is in it.
//
public sealed class LookFile
{
    public const string Extension = ".ssptmm-look";

    // HUNCH: a large photo is a few megabytes; past this the file is not one to pass around.
    public const long MaximumPictureBytes = 16 * 1024 * 1024;

    public int Version { get; set; } = 1;

    public string? AccentColor { get; set; }

    public bool OledBlack { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<CardSize>))]
    public CardSize CardSize { get; set; } = CardSize.Normal;

    // The picture's bytes, and its extension (".png") so it is saved as what it is.
    public byte[]? Background { get; set; }

    public string? BackgroundExtension { get; set; }

    public double BackgroundDarkness { get; set; } = 0.55;

    public double BackgroundBlur { get; set; }

    // Group name -> "#RRGGBB". By name, as a group's id is only this PC's.
    public Dictionary<string, string> GroupColors { get; set; } = [];

    // As AppSettings.ModColors: "id:123", "addon:45" or "name:<folder>" -> "#RRGGBB".
    public Dictionary<string, string> ModColors { get; set; } = [];

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>The look as it is now. The picture is read here; null when there is none or it is too large.</summary>
    public static LookFile Capture(AppSettings settings, ModGroupData groups)
    {
        var look = new LookFile
        {
            AccentColor = Services.ModColors.Normalize(settings.AccentColor),
            OledBlack = settings.OledBlack,
            CardSize = settings.CardSize,
            BackgroundDarkness = settings.BackgroundDarkness,
            BackgroundBlur = settings.BackgroundBlur,
        };

        if (settings.BackgroundImage is { } picture && File.Exists(picture) && new FileInfo(picture).Length <= MaximumPictureBytes)
        {
            look.Background = File.ReadAllBytes(picture);
            look.BackgroundExtension = Path.GetExtension(picture).ToLowerInvariant();
        }

        foreach (var group in groups.Groups)
        {
            if (Services.ModColors.Normalize(group.Color) is { } color) look.GroupColors[group.Name] = color;
        }

        foreach (var (key, value) in settings.ModColors)
        {
            if (Services.ModColors.Normalize(value) is { } color) look.ModColors[key] = color;
        }

        return look;
    }

    /// <summary>Whether the picture in use was too large to go into the file.</summary>
    public static bool PictureTooLarge(AppSettings settings) =>
        settings.BackgroundImage is { } picture && File.Exists(picture) && new FileInfo(picture).Length > MaximumPictureBytes;

    public void Write(string path) => SafeFile.WriteText(path, JsonSerializer.Serialize(this, Indented), keepBackups: false);

    /// <summary>Reads a look file. Throws InvalidDataException when it is not one.</summary>
    public static LookFile Read(string path)
    {
        LookFile? look;
        try
        {
            look = JsonSerializer.Deserialize<LookFile>(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }

        if (look is null || look.Version < 1) throw new InvalidDataException("not a look file");
        if (look.Background is { LongLength: > MaximumPictureBytes }) throw new InvalidDataException("picture too large");
        return look;
    }

    /// <summary>
    /// Puts the look into the settings and the groups. A colour that is not one is left out, and a
    /// group or mod not here is passed over. The picture is not saved here: <paramref name="picture"/>
    /// is where the caller wrote <see cref="Background"/>, or null for none. Returns how many
    /// groups were given a colour.
    /// </summary>
    public int ApplyTo(AppSettings settings, ModGroupData groups, string? picture)
    {
        settings.AccentColor = Services.ModColors.Normalize(AccentColor);
        settings.OledBlack = OledBlack;
        settings.CardSize = Enum.IsDefined(CardSize) ? CardSize : CardSize.Normal;
        settings.BackgroundDarkness = Math.Clamp(double.IsFinite(BackgroundDarkness) ? BackgroundDarkness : 0.55, 0, 0.9);
        settings.BackgroundBlur = Math.Clamp(double.IsFinite(BackgroundBlur) ? BackgroundBlur : 0, 0, 20);
        settings.BackgroundImage = picture;

        settings.ModColors.Clear();
        foreach (var (key, value) in ModColors)
        {
            if (Services.ModColors.Normalize(value) is { } color && !string.IsNullOrWhiteSpace(key)) settings.ModColors[key] = color;
        }

        var colored = 0;
        foreach (var group in groups.Groups)
        {
            var match = GroupColors.FirstOrDefault(p => string.Equals(p.Key.Trim(), group.Name.Trim(), StringComparison.OrdinalIgnoreCase));
            group.Color = match.Key is null ? null : Services.ModColors.Normalize(match.Value);
            if (group.Color is not null) colored++;
        }

        return colored;
    }

    /// <summary>The extension the picture is saved under: its own when it is a picture's, else ".png".</summary>
    public string PictureExtension =>
        BackgroundExtension?.ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp"
            ? BackgroundExtension.ToLowerInvariant()
            : ".png";
}
