using TCFModManager.Core.Services;

namespace TCFModManager.Core.SpModApi;

public sealed class SpModApiOptions
{
    public const string OfficialSite = "https://sp-mod.com";

    // Fork (1.4.3): the site whose /api/v0 the catalogue is read from - sp-mod.com, or a mirror
    // that answers the same API with the same ids (AppSettings.CatalogSite). Set by the App at start,
    // before its client is built. Comments, mod lists and the site's own pages stay sp-mod.com's.
    public static string CatalogSite { get; set; } = OfficialSite;

    public string BaseUrl { get; init; } = CatalogSite;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    // Sent as the User-Agent header. Built from the running assembly's version rather than typed
    // here, which is what stops it going stale - it read "TCFModManager/0.1" while releases were at
    // 1.3.0-beta. Keeping it derived also means the release script only ever has one version to
    // write (build\Directory.Build.props), instead of rewriting a source file too.
    public string UserAgent { get; init; } = $"{SelfMod.ShortName}/{AppVersion.Current}";
}
