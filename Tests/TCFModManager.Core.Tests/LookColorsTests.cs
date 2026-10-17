using TCFModManager.Core.Services;
using Xunit;

namespace TCFModManager.Core.Tests;

// Fork (1.4.3): Options > Look's accent colour and OLED black.
public sealed class LookColorsTests
{
    [Theory]
    [InlineData(0xFF1A9FFF, LookColors.Role.Accent)] // Steam blue
    [InlineData(0xFF66C0F4, LookColors.Role.Accent)] // the legacy link blue
    [InlineData(0xFF21537A, LookColors.Role.Accent)] // the pager's dark blue
    [InlineData(0xFF1B2838, LookColors.Role.Dark)]   // the navy page: a surface, not the accent
    [InlineData(0xFF262B34, LookColors.Role.Dark)]
    [InlineData(0xFF273B52, LookColors.Role.Dark)]   // the cards' navy
    [InlineData(0xFF3D4450, LookColors.Role.None)]   // the buttons' grey
    [InlineData(0xFF5C8699, LookColors.Role.Tint)]   // a muted blue text
    [InlineData(0xFF344352, LookColors.Role.Dark)]   // the popups' navy
    [InlineData(0xFFACB2B8, LookColors.Role.None)]   // text grey
    [InlineData(0xFFA4D007, LookColors.Role.None)]   // Steam green
    [InlineData(0x66000000, LookColors.Role.None)]   // a black shade stays as it is
    public void Colours_are_sorted_into_accent_surface_and_the_rest(uint argb, LookColors.Role role) =>
        Assert.Equal(role, LookColors.RoleOf(argb));

    [Fact]
    public void Steam_blue_and_no_black_change_nothing()
    {
        foreach (var c in new uint[] { 0xFF1A9FFF, 0xFF66C0F4, 0xFF262B34, 0xFFACB2B8, 0x5C1A9FFF })
        {
            Assert.Equal(c, LookColors.Transform(c, null, black: false));
            Assert.Equal(c, LookColors.Transform(c, LookColors.SteamBlue, black: false));
        }
    }

    [Fact]
    public void The_accent_itself_comes_out_as_chosen()
    {
        foreach (var accent in new uint[] { 0xFF5BA32B, 0xFFE8862A, 0xFF8E63E0, 0xFFD9443A, 0xFFE0559A })
        {
            var drawn = LookColors.Transform(LookColors.SteamBlue, accent, black: false);
            AssertNear(accent, drawn);
        }
    }

    [Fact]
    public void A_shifted_blue_keeps_its_transparency_and_greys_stay_grey()
    {
        Assert.Equal(0x5Cu, LookColors.Transform(0x5C1A9FFF, 0xFF5BA32B, black: false) >> 24);
        Assert.Equal(0xFFACB2B8u, LookColors.Transform(0xFFACB2B8, 0xFF5BA32B, black: true));
    }

    [Fact]
    public void Steam_navies_take_the_accent_hue_and_keep_their_lightness()
    {
        var purple = 0xFF8E63E0u;
        var card = LookColors.Transform(0xFF273B52, purple, black: false);
        Assert.NotEqual(0xFF273B52u, card);
        Assert.True(((card >> 16) & 0xFF) > (card & 0xFF) - 0x30, "the card should lean to purple, red near blue");
        var sum = ((card >> 16) & 0xFF) + ((card >> 8) & 0xFF) + (card & 0xFF);
        Assert.InRange((int)sum, 0x27 + 0x3B + 0x52 - 40, 0x27 + 0x3B + 0x52 + 40);

        Assert.Equal(0xFF3D4450u, LookColors.Transform(0xFF3D4450, purple, black: false)); // a grey is left
    }

    [Fact]
    public void Black_with_an_accent_still_takes_the_pages_to_black()
    {
        Assert.Equal(0xFF000000u, LookColors.Transform(0xFF1B2838, 0xFF5BA32B, black: true));
        var card = LookColors.Transform(0xFF273B52, 0xFF5BA32B, black: true);
        Assert.True(((card >> 8) & 0xFF) >= (card & 0xFF), "a dimmed card in the accent's green");
    }

    [Fact]
    public void Black_takes_the_pages_to_black_and_dims_other_surfaces()
    {
        Assert.Equal(0xFF000000u, LookColors.Transform(0xFF262B34, null, black: true));
        Assert.Equal(0xFF000000u, LookColors.Transform(0xFF1B2838, null, black: true));

        var dialog = LookColors.Transform(0xFF25282E, null, black: true);
        Assert.NotEqual(0xFF000000u, dialog);
        Assert.True((dialog & 0xFF) < 0x2E);

        Assert.Equal(0xFF1A9FFFu, LookColors.Transform(0xFF1A9FFF, null, black: true)); // the accent is not a surface
    }

    [Theory]
    [InlineData("#1A9FFF", 0xFF1A9FFF)]
    [InlineData("5C1A9FFF", 0x5C1A9FFF)]
    [InlineData(" #e0559a ", 0xFFE0559A)]
    public void Hex_is_read_with_and_without_its_hash(string text, uint argb)
    {
        Assert.True(LookColors.TryParse(text, out var parsed));
        Assert.Equal(argb, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#FFF")]
    [InlineData("#GG0000")]
    [InlineData("blue")]
    public void Anything_else_is_not_a_colour(string? text) => Assert.False(LookColors.TryParse(text, out _));

    private static void AssertNear(uint expected, uint actual)
    {
        for (var shift = 0; shift <= 16; shift += 8)
            Assert.InRange((int)((actual >> shift) & 0xFF), (int)((expected >> shift) & 0xFF) - 3, (int)((expected >> shift) & 0xFF) + 3);
    }
}
