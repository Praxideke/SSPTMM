using System.Text.Json;
using System.Text.RegularExpressions;

namespace TCFModManager.Core.Services;

/// <summary>Where SPT's two profile-cleaning switches stand in its core.json.</summary>
public enum SptProfileFixState
{
    /// <summary>No core.json, or one without both switches - nothing this can change.</summary>
    NotFound,

    /// <summary>At least one of them is off.</summary>
    Off,

    /// <summary>Both on: SPT removes what's missing from a profile when the server starts.</summary>
    On,
}

//
// Fork (SSPTMM 1.4.2): SPT's own fix for a profile it marked invalid. core.json's "fixes" holds
// removeModItemsFromProfile and removeInvalidTradersFromProfile; with both on, the server takes the
// items and traders no installed mod adds out of a profile as it loads it, instead of refusing it.
//
// Turned on by changing just those two words in the file, so the rest of it - its layout and
// anything else changed by hand - stays as it was. The file as it was is kept beside it first.
//
public static partial class SptProfileFixes
{
    public static readonly string[] Switches = ["removeModItemsFromProfile", "removeInvalidTradersFromProfile"];

    /// <summary>The name of the copy kept beside core.json before it is changed.</summary>
    public const string BackupSuffix = ".before-ssptmm";

    [GeneratedRegex(@"(""(?<name>removeModItemsFromProfile|removeInvalidTradersFromProfile)""\s*:\s*)(?<value>true|false)")]
    private static partial Regex Switch();

    /// <summary>The server's core.json: SPT 4 keeps it in SPT_Data/configs, SPT 3 in
    /// SPT_Data/Server/configs. Null when neither is there.</summary>
    public static string? ConfigPath(string? installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath)
            || !SptInstallationService.TryGetServerRoot(installPath, out var serverRoot)) return null;

        var server = Path.Combine(installPath, serverRoot);
        return new[]
            {
                Path.Combine(server, "SPT_Data", "configs", "core.json"),
                Path.Combine(server, "SPT_Data", "Server", "configs", "core.json"),
            }
            .FirstOrDefault(File.Exists);
    }

    public static SptProfileFixState Read(string? configPath)
    {
        if (configPath is null || !File.Exists(configPath)) return SptProfileFixState.NotFound;

        try
        {
            return StateOf(File.ReadAllText(configPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Debug("Diagnose", $"couldn't read {configPath}: {ex.Message}");
            return SptProfileFixState.NotFound;
        }
    }

    public static SptProfileFixState StateOf(string json)
    {
        var values = Switch().Matches(json)
            .GroupBy(m => m.Groups["name"].Value)
            .ToDictionary(g => g.Key, g => g.All(m => m.Groups["value"].Value == "true"));

        if (Switches.Any(s => !values.ContainsKey(s))) return SptProfileFixState.NotFound;
        return values.Values.All(on => on) ? SptProfileFixState.On : SptProfileFixState.Off;
    }

    /// <summary>The same text with both switches on. Null when it doesn't hold both, or the result
    /// would not read as JSON - then nothing is written.</summary>
    public static string? TurnedOn(string json)
    {
        if (StateOf(json) == SptProfileFixState.NotFound) return null;

        var changed = Switch().Replace(json, m => m.Groups[1].Value + "true");
        try
        {
            using var _ = JsonDocument.Parse(changed,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException)
        {
            return null;
        }

        return changed;
    }

    /// <summary>Turns both switches on, keeping the file as it was beside it (core.json.before-ssptmm,
    /// the first time only). Null when done; empty when the file doesn't hold both switches as
    /// expected (nothing written); otherwise the error that stopped it.</summary>
    public static string? TurnOn(string configPath)
    {
        try
        {
            var json = File.ReadAllText(configPath);
            if (TurnedOn(json) is not { } changed) return string.Empty;

            var backup = configPath + BackupSuffix;
            if (!File.Exists(backup)) File.Copy(configPath, backup);

            SafeFile.WriteText(configPath, changed, keepBackups: false);
            AppLog.Info("Diagnose", $"turned on {string.Join(", ", Switches)} in {configPath}");
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("Diagnose", $"couldn't change {configPath}: {ex.Message}");
            return ex.Message;
        }
    }
}
