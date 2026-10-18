using TCFModManager.Core.Models;
using TCFModManager.Core.Services;
using Xunit;

namespace TCFModManager.Core.Tests;

//
// Two mods in one folder (SPT Stance Overhaul and SPT Realism Common Lib, both in
// BepInEx\plugins\Realism): switching one moves only its own files, and Undo and switching back
// leave no empty folders behind.
//
public class SharedFolderDisableTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SsptmmSharedFolder_" + Guid.NewGuid());

    private string Live => Path.Combine(_root, "BepInEx", "plugins", "Realism");

    private string Off => Path.Combine(_root, "BepInEx", "plugins.disabled", "Realism");

    public SharedFolderDisableTests()
    {
        Directory.CreateDirectory(Path.Combine(Live, "data"));
        File.WriteAllText(Path.Combine(Live, "spt-realism-common-lib.dll"), "lib");
        File.WriteAllText(Path.Combine(Live, "spt-stance-overhaul.dll"), "stance");
        File.WriteAllText(Path.Combine(Live, "data", "curves.json"), "{}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static InstalledMod Entry(string folder, bool disabled) => new()
    {
        Name = "Realism",
        Target = InstalledModTarget.Client,
        FolderPath = folder,
        IsDisabled = disabled,
    };

    private static readonly string[] StanceFiles = ["spt-stance-overhaul.dll", "data/curves.json"];

    [Fact]
    public void Disabling_one_moves_only_its_files_and_enabling_tidies_up()
    {
        var off = ModDisableService.ApplyFiles([(Entry(Live, false), StanceFiles)], disable: true);

        Assert.Empty(off.Failed);
        Assert.Equal(2, off.Moved.Count);
        Assert.True(File.Exists(Path.Combine(Live, "spt-realism-common-lib.dll")));
        Assert.False(File.Exists(Path.Combine(Live, "spt-stance-overhaul.dll")));
        Assert.False(Directory.Exists(Path.Combine(Live, "data")));
        Assert.True(File.Exists(Path.Combine(Off, "data", "curves.json")));

        var on = ModDisableService.ApplyFiles([(Entry(Off, true), StanceFiles)], disable: false);

        Assert.Empty(on.Failed);
        Assert.True(File.Exists(Path.Combine(Live, "data", "curves.json")));
        Assert.False(Directory.Exists(Path.Combine(_root, "BepInEx", "plugins.disabled")));
    }

    [Fact]
    public void Undo_puts_the_files_back_and_tidies_up()
    {
        var off = ModDisableService.ApplyFiles([(Entry(Live, false), StanceFiles)], disable: true);

        var back = ModDisableService.Revert(off.Moved);

        Assert.Empty(back.Failed);
        Assert.True(File.Exists(Path.Combine(Live, "spt-stance-overhaul.dll")));
        Assert.True(File.Exists(Path.Combine(Live, "data", "curves.json")));
        Assert.False(Directory.Exists(Path.Combine(_root, "BepInEx", "plugins.disabled")));
    }

    [Fact]
    public void The_live_container_is_never_removed()
    {
        ModDisableService.ApplyFiles(
            [(Entry(Live, false), ["spt-realism-common-lib.dll", .. StanceFiles])], disable: true);

        Assert.False(Directory.Exists(Live));
        Assert.True(Directory.Exists(Path.Combine(_root, "BepInEx", "plugins")));
    }
}
