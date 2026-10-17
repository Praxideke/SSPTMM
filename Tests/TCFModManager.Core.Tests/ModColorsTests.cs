using TCFModManager.Core.Services;
using Xunit;

namespace TCFModManager.Core.Tests;

// Fork (1.4.3): colours for mods and groups on Subscribed items.
public sealed class ModColorsTests
{
    [Fact]
    public void The_palette_has_ten_colours_none_alike()
    {
        Assert.Equal(10, ModColors.Palette.Count);
        Assert.Equal(10, ModColors.Palette.Select(s => s.Hex).Distinct().Count());
        Assert.Equal(10, ModColors.Palette.Select(s => s.Key).Distinct().Count());
    }

    [Theory]
    [InlineData("#e5534b", "#E5534B")]
    [InlineData("E5534B", "#E5534B")]
    [InlineData("#FFE5534B", "#E5534B")]
    [InlineData("#80E5534B", null)] // see-through is not a colour for a card's edge
    [InlineData("red", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normalize_gives_six_digits_in_capitals(string? text, string? expected) =>
        Assert.Equal(expected, ModColors.Normalize(text));

    [Fact]
    public void SwatchOf_knows_the_palettes_own_and_not_others()
    {
        Assert.Equal("red", ModColors.SwatchOf("#e5534b")?.Key);
        Assert.Null(ModColors.SwatchOf("#123456"));
        Assert.Null(ModColors.SwatchOf(null));
    }

    [Theory]
    [InlineData("#E5534B", "red", true)]
    [InlineData("#E5534B", "RED", true)]
    [InlineData("#E5534B", "Rot", true)]       // by its name as shown
    [InlineData("#E5534B", "#e5534b", true)]
    [InlineData("#E5534B", "blue", false)]
    [InlineData("#E5534B", "none", false)]
    [InlineData(null, "none", true)]
    [InlineData(null, "red", false)]
    [InlineData("#123456", "#123456", true)]   // a colour of your own, by its hex
    [InlineData("#123456", "red", false)]
    [InlineData("#123456", "", true)]          // "color:" alone asks for nothing yet
    public void Matches_a_colour_by_key_name_hex_or_none(string? color, string query, bool expected) =>
        Assert.Equal(expected, ModColors.Matches(color, query, s => s.Key == "red" ? "Rot" : s.Key));
}
