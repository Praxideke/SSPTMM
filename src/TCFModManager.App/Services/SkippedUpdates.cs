using TCFModManager.Core.Services;

namespace TCFModManager.App.Services;

//
// Fork (1.4.2): "Skip this update" on a mod's right-click menu. A skipped version is not an update
// anywhere - the card, the Updates filter, Update all, the item page and the background check all
// read InstalledModCardViewModel.UpdateAvailable, which BuildFrom sets false for it - until a
// newer version than the one skipped comes out. Kept in AppSettings.SkippedUpdates.
//
public static class SkippedUpdates
{
    private static readonly object Gate = new();
    private static Dictionary<string, string>? _skipped;

    // Read once; the background update check asks from another thread.
    private static Dictionary<string, string> Skipped =>
        _skipped ??= new Dictionary<string, string>(new SettingsService().Load().SkippedUpdates, StringComparer.OrdinalIgnoreCase);

    private static string KeyFor(int id, bool isAddon) => (isAddon ? "addon:" : "id:") + id;

    /// <summary>The version of this mod being skipped, if any.</summary>
    public static string? For(int? id, bool isAddon)
    {
        if (id is not { } known) return null;

        lock (Gate) return Skipped.GetValueOrDefault(KeyFor(known, isAddon));
    }

    /// <summary>True when exactly this version of this mod is skipped.</summary>
    public static bool Skips(int? id, bool isAddon, string? version) =>
        version is not null && string.Equals(For(id, isAddon), version, StringComparison.OrdinalIgnoreCase);

    /// <summary>Skips this version of the mod; null stops skipping.</summary>
    public static void Set(int id, bool isAddon, string? version)
    {
        lock (Gate)
        {
            if (version is null) Skipped.Remove(KeyFor(id, isAddon));
            else Skipped[KeyFor(id, isAddon)] = version;

            var service = new SettingsService();
            var settings = service.Load();
            settings.SkippedUpdates = new Dictionary<string, string>(Skipped);
            service.Save(settings);
        }

        AppLog.Info("Updates", version is null ? $"{KeyFor(id, isAddon)}: no longer skipping" : $"{KeyFor(id, isAddon)}: skipping {version}");
    }
}
