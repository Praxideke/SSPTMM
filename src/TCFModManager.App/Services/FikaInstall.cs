using System.IO;
using TCFModManager.Core.Models;
using TCFModManager.Core.Services;

namespace TCFModManager.App.Services;

//
// Fork (1.4.3): whether an install runs Fika - any plugin of Project Fika's ("com.fika.*") loaded
// from it, or an enabled server mod named for it ("fika-server", "Fika.Server"...) or carrying such
// a ModGuid. Read at most every few minutes: a collection queues many mods at once, and the Play
// page asks on every refresh.
//
public static class FikaInstall
{
    public readonly record struct Found(bool Client, bool Server)
    {
        public bool Any => Client || Server;
    }

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private static readonly object Gate = new();
    private static (string Path, Found Found, DateTime At)? _last;

    /// <summary>What was found for this install lately, without looking again; null when it has not been looked at.</summary>
    public static Found? Known(string? installPath)
    {
        lock (Gate)
        {
            return _last is { } last && string.Equals(last.Path, installPath, StringComparison.OrdinalIgnoreCase)
                && DateTime.UtcNow - last.At < Lifetime
                ? last.Found
                : null;
        }
    }

    public static Task<Found> ReadAsync(string installPath, bool fresh = false) => Task.Run(() => Read(installPath, fresh));

    public static Found Read(string installPath, bool fresh = false)
    {
        if (!fresh && Known(installPath) is { } known) return known;

        Found found;
        try
        {
            // Its client plugins, or - on a machine that only hosts - its server mod's folder.
            var client = InstalledModScanner.LoadedPluginGuids(installPath).Any(IsFikaGuid);
            var server = InstalledModScanner.Scan(installPath).Any(m => m is { Target: InstalledModTarget.Server, IsDisabled: false }
                && (IsFikaServer(m.Name) || IsFikaServer(Path.GetFileName(m.FolderPath)) || IsFikaGuid(m.Guid)));
            found = new Found(client, server);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Debug("Fika", $"couldn't look for Fika in the install: {ex.Message}");
            found = default;
        }

        lock (Gate) _last = (installPath, found, DateTime.UtcNow);
        return found;
    }

    private static bool IsFikaGuid(string? guid) => guid?.StartsWith("com.fika.", StringComparison.OrdinalIgnoreCase) ?? false;

    // Fika's own server mod, not a mod that merely mentions Fika.
    private static bool IsFikaServer(string? name) =>
        name is not null
        && name.StartsWith("fika", StringComparison.OrdinalIgnoreCase)
        && name.Contains("server", StringComparison.OrdinalIgnoreCase);
}
