using System.Text.Json;

namespace TCFModManager.Core.Services;

//
// Fork (1.4.3): where a Fika host's server listens. Fika takes its address and port from its own
// fika.jsonc ("server" > "SPT" > "http") and ignores http.json's, which it overrides at start; the
// file is written the first time the server runs with Fika. Layout from Fika's wiki, "Server"
// configuration page.
//
public sealed record FikaServerConfig(string? Ip, int? Port, string? BackendIp, int? BackendPort)
{
    // The usual folder, then any other server mod folder of Fika's that has the file.
    public static string? Find(string? serverRoot)
    {
        if (string.IsNullOrWhiteSpace(serverRoot)) return null;

        var mods = Path.Combine(serverRoot, "user", "mods");
        var usual = Path.Combine(mods, "fika-server", "assets", "configs", "fika.jsonc");
        if (File.Exists(usual)) return usual;
        if (!Directory.Exists(mods)) return null;

        try
        {
            return Directory.EnumerateDirectories(mods)
                .Where(d => Path.GetFileName(d).StartsWith("fika", StringComparison.OrdinalIgnoreCase))
                .Select(d => Path.Combine(d, "assets", "configs", "fika.jsonc"))
                .FirstOrDefault(File.Exists);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Null when there is no file yet, or it can't be read as JSON with comments.
    public static FikaServerConfig? Read(string? serverRoot)
    {
        if (Find(serverRoot) is not { } path) return null;

        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Debug("Fika", $"couldn't read {path}: {ex.Message}");
            return null;
        }
    }

    public static FikaServerConfig? Parse(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (!Child(doc.RootElement, "server", out var server) || !Child(server, "SPT", out var spt) || !Child(spt, "http", out var http))
                return new FikaServerConfig(null, null, null, null);

            return new FikaServerConfig(Text(http, "ip"), Number(http, "port"), Text(http, "backendIp"), Number(http, "backendPort"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Bound to this PC alone: nobody else can reach it.
    public bool OnlyThisPc => Ip is { } ip && (ip == "localhost" || ip.StartsWith("127.", StringComparison.Ordinal) || ip == "::1");

    private static bool Child(JsonElement parent, string name, out JsonElement child)
    {
        child = default;
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out child) && child.ValueKind == JsonValueKind.Object;
    }

    private static string? Text(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;

    private static int? Number(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            && number is > 0 and <= 65535
            ? number
            : null;
}
