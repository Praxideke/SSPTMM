using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TCFModManager.App.Behaviors;

//
// Fork (1.4.2): where the window's keys go on a page (Help > Keyboard shortcuts). Ctrl+F goes to
// the box marked Shortcuts.Search="True"; F5 runs Shortcuts.Refresh, the command behind the page's
// own Rescan or Refresh button. MainWindow looks only at what is on screen: the mod, collection or
// author page over the others when one is open, else the page under them.
//
public static class Shortcuts
{
    public static readonly DependencyProperty SearchProperty = DependencyProperty.RegisterAttached(
        "Search", typeof(bool), typeof(Shortcuts), new PropertyMetadata(false));

    public static bool GetSearch(DependencyObject element) => (bool)element.GetValue(SearchProperty);

    public static void SetSearch(DependencyObject element, bool value) => element.SetValue(SearchProperty, value);

    public static readonly DependencyProperty RefreshProperty = DependencyProperty.RegisterAttached(
        "Refresh", typeof(ICommand), typeof(Shortcuts), new PropertyMetadata(null));

    public static ICommand? GetRefresh(DependencyObject element) => (ICommand?)element.GetValue(RefreshProperty);

    public static void SetRefresh(DependencyObject element, ICommand? value) => element.SetValue(RefreshProperty, value);

    /// <summary>The marked search box on screen under <paramref name="scope"/>, if any.</summary>
    public static TextBox? SearchBoxIn(DependencyObject scope) =>
        First(scope, e => e is TextBox { IsEnabled: true } && GetSearch(e)) as TextBox;

    /// <summary>The refresh of the page on screen under <paramref name="scope"/>, if it has one.</summary>
    public static ICommand? RefreshIn(DependencyObject scope) =>
        First(scope, e => GetRefresh(e) is not null) is { } element ? GetRefresh(element) : null;

    // Depth first, skipping whatever is not on screen - a page the navigation keeps but has left,
    // a collapsed panel.
    private static DependencyObject? First(DependencyObject node, Func<DependencyObject, bool> test)
    {
        if (node is UIElement { IsVisible: false }) return null;
        if (test(node)) return node;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            if (First(VisualTreeHelper.GetChild(node, i), test) is { } found) return found;
        }

        return null;
    }
}
