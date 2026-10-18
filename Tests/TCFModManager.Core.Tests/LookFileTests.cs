using TCFModManager.Core.Models;
using TCFModManager.Core.Services;
using Xunit;

namespace TCFModManager.Core.Tests;

// Fork (1.4.3): Options > Look saved to a file and read back.
public sealed class LookFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ssptmm-look-" + Guid.NewGuid().ToString("N"));

    public LookFileTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void A_look_goes_to_a_file_and_back_with_its_picture_and_colours()
    {
        var picture = Path.Combine(_folder, "background-1.JPG");
        File.WriteAllBytes(picture, [1, 2, 3, 4]);
        var settings = new AppSettings
        {
            AccentColor = "#8E63E0",
            OledBlack = true,
            CardSize = CardSize.Roomy,
            BackgroundImage = picture,
            BackgroundDarkness = 0.3,
            BackgroundBlur = 12,
            ModColors = { ["id:1"] = "#E5534B", ["name:broken"] = "red" },
        };
        var groups = new ModGroupData
        {
            Groups = { new ModGroup { Id = Guid.NewGuid(), Name = "Graphics", Color = "#39C5BB" }, new ModGroup { Id = Guid.NewGuid(), Name = "Plain" } },
        };

        var path = Path.Combine(_folder, "mine" + LookFile.Extension);
        LookFile.Capture(settings, groups).Write(path);
        var look = LookFile.Read(path);

        Assert.Equal("#8E63E0", look.AccentColor);
        Assert.True(look.OledBlack);
        Assert.Equal(CardSize.Roomy, look.CardSize);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, look.Background);
        Assert.Equal(".jpg", look.PictureExtension);
        Assert.Equal(0.3, look.BackgroundDarkness);
        Assert.Equal(12, look.BackgroundBlur);
        Assert.Equal(new Dictionary<string, string> { ["Graphics"] = "#39C5BB" }, look.GroupColors);
        Assert.Equal(new Dictionary<string, string> { ["id:1"] = "#E5534B" }, look.ModColors);
    }

    [Fact]
    public void Applying_matches_groups_by_name_and_clears_the_rest()
    {
        var look = new LookFile
        {
            AccentColor = "5ba32b",
            CardSize = CardSize.Compact,
            BackgroundDarkness = 5,
            BackgroundBlur = double.NaN,
            GroupColors = { ["graphics "] = "#39C5BB", ["Gone"] = "#E5534B" },
            ModColors = { ["id:2"] = "#4493F8", ["id:3"] = "nope" },
        };
        var settings = new AppSettings { ModColors = { ["id:9"] = "#E5534B" } };
        var groups = new ModGroupData
        {
            Groups = { new ModGroup { Id = Guid.NewGuid(), Name = "Graphics" }, new ModGroup { Id = Guid.NewGuid(), Name = "Fika", Color = "#A371F7" } },
        };

        var colored = look.ApplyTo(settings, groups, picture: "C:\\pic.png");

        Assert.Equal(1, colored);
        Assert.Equal("#5BA32B", settings.AccentColor);
        Assert.Equal(CardSize.Compact, settings.CardSize);
        Assert.Equal(0.9, settings.BackgroundDarkness);
        Assert.Equal(0, settings.BackgroundBlur);
        Assert.Equal("C:\\pic.png", settings.BackgroundImage);
        Assert.Equal(new Dictionary<string, string> { ["id:2"] = "#4493F8" }, settings.ModColors);
        Assert.Equal("#39C5BB", groups.Groups[0].Color);
        Assert.Null(groups.Groups[1].Color);
    }

    [Fact]
    public void A_file_that_is_not_a_look_is_refused()
    {
        var path = Path.Combine(_folder, "bad" + LookFile.Extension);
        File.WriteAllText(path, "{ not json");
        Assert.Throws<InvalidDataException>(() => LookFile.Read(path));

        File.WriteAllText(path, "{ \"Version\": 0 }");
        Assert.Throws<InvalidDataException>(() => LookFile.Read(path));
    }

    [Fact]
    public void A_picture_that_is_gone_is_left_out()
    {
        var settings = new AppSettings { BackgroundImage = Path.Combine(_folder, "missing.png") };
        var look = LookFile.Capture(settings, new ModGroupData());
        Assert.Null(look.Background);
        Assert.False(LookFile.PictureTooLarge(settings));
    }
}
