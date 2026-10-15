using TCFModManager.Core.Services;
using Xunit;

namespace TCFModManager.Core.Tests;

// The window's back and forward arrows: a browser's history, with closing an item page counted
// as a step back.
public class NavigationHistoryTests
{
    private static NavigationHistory<string> Visited(params string[] places)
    {
        var history = new NavigationHistory<string>();
        foreach (var place in places) history.Visit(place);
        return history;
    }

    [Fact]
    public void Back_and_forward_walk_the_places_without_changing_them()
    {
        var history = Visited("Home", "Browse", "Item");

        Assert.Equal("Browse", history.Back());
        Assert.Equal("Home", history.Back());
        Assert.False(history.CanGoBack);
        Assert.Null(history.Back());
        Assert.Equal("Browse", history.Forward());
        Assert.Equal("Item", history.Forward());
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void Going_somewhere_new_drops_what_was_ahead()
    {
        var history = Visited("Home", "Browse", "Item");
        history.Back();
        history.Back();

        history.Visit("Play");

        Assert.False(history.CanGoForward);
        Assert.Equal("Home", history.Back());
    }

    [Fact]
    public void The_same_place_twice_is_one_entry()
    {
        var history = Visited("Home", "Home");

        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void Closing_back_to_the_place_before_is_a_step_back()
    {
        var history = Visited("Browse", "Item");

        history.Return("Browse");

        Assert.Equal("Browse", history.Current);
        Assert.Equal("Item", history.Forward());
    }

    [Fact]
    public void Closing_to_anywhere_else_is_somewhere_new()
    {
        var history = Visited("Home", "Browse", "Item");

        history.Return("Home");

        Assert.Equal("Home", history.Current);
        Assert.False(history.CanGoForward);
        Assert.Equal("Item", history.Back());
    }

    [Fact]
    public void Only_the_last_places_are_kept()
    {
        var history = new NavigationHistory<int>(capacity: 3);
        for (var i = 1; i <= 5; i++) history.Visit(i);

        Assert.Equal(4, history.Back());
        Assert.Equal(3, history.Back());
        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void Dropping_a_place_stands_on_the_one_before()
    {
        var history = Visited("Home", "Browse", "Gone");

        history.DropCurrent();

        Assert.Equal("Browse", history.Current);
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void Jumping_to_an_entry_keeps_the_rest()
    {
        var history = Visited("Home", "Browse", "Item", "Author");

        Assert.Equal("Browse", history.MoveTo(1));
        Assert.Equal(1, history.Index);
        Assert.Equal(4, history.Entries.Count);
        Assert.Equal("Author", history.MoveTo(3));
        Assert.Null(history.MoveTo(3));
        Assert.Null(history.MoveTo(9));
        Assert.Equal("Author", history.Current);
    }
}
