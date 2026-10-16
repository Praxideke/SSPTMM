using TCFModManager.Core.Services;
using Xunit;

namespace TCFModManager.Core.Tests;

// Fork (1.4.2): an archive of no known layout, placed where the user said it goes.
public sealed class ArchivePlacementTests : IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), "placement-" + Guid.NewGuid().ToString("N"));

    public ArchivePlacementTests() => Directory.CreateDirectory(_work);

    public void Dispose() => Directory.Delete(_work, recursive: true);

    private void Write(string relative)
    {
        var path = Path.Combine(_work, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
    }

    [Fact]
    public void LooseFiles_GoInAFolderNamedForTheMod()
    {
        Write("Thing.dll");
        Write("readme.txt");

        var root = ArchiveLayout.PlaceAs(_work, _work, ArchivePlacement.ClientPlugin, "My: Mod");

        Assert.True(File.Exists(Path.Combine(root, "BepInEx", "plugins", "My_ Mod", "Thing.dll")));
        Assert.True(File.Exists(Path.Combine(root, "BepInEx", "plugins", "My_ Mod", "readme.txt")));
        Assert.Contains("BepInEx", Directory.GetFileSystemEntries(root).Select(Path.GetFileName));
    }

    [Fact]
    public void OneFolder_IsTheModsOwnFolder()
    {
        Write(Path.Combine("ServerThing", "package.json"));
        Write(Path.Combine("ServerThing", "src", "mod.js"));

        var described = ArchiveLayout.Describe(_work, "Server Thing", "1.0.0");
        Assert.Equal(ArchivePlacement.ServerMod, described.Likely);
        Assert.Equal(2, described.FileCount);

        var root = ArchiveLayout.PlaceAs(_work, _work, ArchivePlacement.ServerMod, "Server Thing");

        Assert.True(File.Exists(Path.Combine(root, "user", "mods", "ServerThing", "src", "mod.js")));
    }

    [Fact]
    public void OneFolder_KeepsItsNameWhenTheInstallHasGoneInsideIt()
    {
        Write(Path.Combine("SAIN", "SAIN.dll"));
        Write(Path.Combine("SAIN", "Presets", "default.json"));

        var contentRoot = ArchiveLayout.FindContentRoot(_work);
        var root = ArchiveLayout.PlaceAs(contentRoot, _work, ArchivePlacement.ClientPlugin, "Solarint's AI Modifications");

        Assert.True(File.Exists(Path.Combine(root, "BepInEx", "plugins", "SAIN", "Presets", "default.json")));
    }

    [Fact]
    public void OneFolder_NamedWithAVersion_GoesInAFolderNamedForTheMod()
    {
        Write(Path.Combine("Thing-1.2.0", "Thing.dll"));
        Write(Path.Combine("Thing-1.2.0", "readme.txt"));

        var contentRoot = ArchiveLayout.FindContentRoot(_work);
        var root = ArchiveLayout.PlaceAs(contentRoot, _work, ArchivePlacement.ClientPlugin, "Thing");

        Assert.True(File.Exists(Path.Combine(root, "BepInEx", "plugins", "Thing", "Thing.dll")));
    }

    [Fact]
    public void Describe_ADllAloneIsLikelyAClientMod()
    {
        Write("Thing.dll");

        Assert.Equal(ArchivePlacement.ClientPlugin, ArchiveLayout.Describe(_work, "Thing", null).Likely);
    }
}
