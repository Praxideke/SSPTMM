using TCFModManager.Core.Services;
using Xunit;

namespace TCFModManager.Core.Tests;

//
// SPT's two profile-cleaning switches in core.json: read, and turned on by changing only those two
// words, with the file as it was kept beside it.
//
public class SptProfileFixesTests : IDisposable
{
    // The "fixes" block as SPT 4.1.6 ships it, with both switches off.
    private const string Shipped = """
        {
          "projectName": "SPT",
          "fixes": {
            "fixShotgunDispersion": true,
            "removeModItemsFromProfile": false,
            "removeInvalidTradersFromProfile": false,
            "fixProfileBreakingInventoryItemIssues": false
          }
        }
        """;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SsptmmProfileFixes_" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Reads_off_on_and_missing()
    {
        Assert.Equal(SptProfileFixState.Off, SptProfileFixes.StateOf(Shipped));
        Assert.Equal(SptProfileFixState.Off, SptProfileFixes.StateOf(Shipped.Replace("\"removeModItemsFromProfile\": false", "\"removeModItemsFromProfile\": true")));
        Assert.Equal(SptProfileFixState.On, SptProfileFixes.StateOf(Shipped.Replace(": false", ": true")));
        Assert.Equal(SptProfileFixState.NotFound, SptProfileFixes.StateOf("""{ "fixes": { "removeModItemsFromProfile": false } }"""));
    }

    [Fact]
    public void Turning_on_changes_only_the_two_switches()
    {
        var changed = SptProfileFixes.TurnedOn(Shipped);

        Assert.Equal(
            Shipped.Replace("\"removeModItemsFromProfile\": false", "\"removeModItemsFromProfile\": true")
                .Replace("\"removeInvalidTradersFromProfile\": false", "\"removeInvalidTradersFromProfile\": true"),
            changed);
        Assert.Equal(SptProfileFixState.On, SptProfileFixes.StateOf(changed!));
    }

    [Fact]
    public void A_file_without_both_switches_is_left_alone()
    {
        Assert.Null(SptProfileFixes.TurnedOn("""{ "fixes": { "removeModItemsFromProfile": false } }"""));
    }

    [Fact]
    public void TurnOn_keeps_the_file_as_it_was_beside_it()
    {
        Directory.CreateDirectory(_directory);
        var config = Path.Combine(_directory, "core.json");
        File.WriteAllText(config, Shipped);

        Assert.Null(SptProfileFixes.TurnOn(config));

        Assert.Equal(SptProfileFixState.On, SptProfileFixes.Read(config));
        Assert.Equal(Shipped, File.ReadAllText(config + SptProfileFixes.BackupSuffix));
    }
}
