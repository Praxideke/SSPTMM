using System.Windows;
using System.Windows.Controls;
using TCFModManager.App.ViewModels;

namespace TCFModManager.App.Views;

// The arrow beside an Update all and its list of waiting updates - see the header of the XAML.
public partial class UpdateListButton : UserControl
{
    public static readonly DependencyProperty ArrowStyleProperty =
        DependencyProperty.Register(nameof(ArrowStyle), typeof(Style), typeof(UpdateListButton));

    /// <summary>The arrow's look - the Update all button's beside it.</summary>
    public Style? ArrowStyle
    {
        get => (Style?)GetValue(ArrowStyleProperty);
        set => SetValue(ArrowStyleProperty, value);
    }

    /// <summary>Just before the list opens: the page fills it with what is waiting now.</summary>
    public event EventHandler? Opening;

    private UpdateList? _list;
    private DateTime _closedAt;

    public UpdateListButton()
    {
        InitializeComponent();
        DataContextChanged += (_, e) => Hook(e.NewValue as UpdateList);
    }

    // A question an update asked closes the list, so it opens again after.
    private void Hook(UpdateList? list)
    {
        if (_list is not null) _list.Acted -= OnActed;
        _list = list;
        if (_list is not null) _list.Acted += OnActed;
    }

    private void OnActed(object? sender, EventArgs e)
    {
        if (!ListPopup.IsOpen && _list is { Rows.Count: > 0 }) Open();
    }

    /// <summary>Opens the list - from the arrow, or a right-click on Update all.</summary>
    public void Open()
    {
        if (_list is null) return;

        Opening?.Invoke(this, EventArgs.Empty);

        // Its right edge under the arrow's, so it opens into the window rather than off its side.
        ListPopup.HorizontalOffset = Arrow.ActualWidth - ListPanel.Width;
        ListPopup.IsOpen = true;
    }

    // A click on the arrow while the list is open closes it: the popup has already closed itself on
    // that press, so it is not opened again straight after.
    private void Arrow_Click(object sender, RoutedEventArgs e)
    {
        if (DateTime.UtcNow - _closedAt < TimeSpan.FromMilliseconds(250)) return;
        Open();
    }

    private void ListPopup_Closed(object? sender, EventArgs e)
    {
        _closedAt = DateTime.UtcNow;
        _list?.Close();
    }

    private void Downloads_Click(object sender, RoutedEventArgs e)
    {
        ListPopup.IsOpen = false;
        AppNavigation.Navigate(typeof(DownloadsPage));
    }
}
