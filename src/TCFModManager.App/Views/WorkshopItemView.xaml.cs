using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TCFModManager.App.Services;
using TCFModManager.App.ViewModels;
using TCFModManager.Core.Services;

namespace TCFModManager.App.Views;

//
// The Workshop item page. MainWindow shows it for every mod opened anywhere in the app; a required
// item opened from it replaces it, and Esc walks back through those one at a time before closing.
// The mouse's back button and Alt+Left are the window's (MainWindow's history), which does the same.
//
// Fork (1.4.3): each item left keeps its tab and how far down it was, and comes back to them when
// it is returned to - by Esc, or the window's arrows - rather than at its top. Opened afresh from a
// card or a link, it starts at the top, as on Steam.
//
public partial class WorkshopItemView : UserControl
{
    // The item pages this one was opened over, most recent last.
    private readonly Stack<ModDetailsRequest> _history = new();

    private ModDetailsRequest? _current;

    private WorkshopItemViewModel? _viewModel;

    // Where each item was left, by the request that opened it.
    private readonly Dictionary<ModDetailsRequest, Spot> _spots = [];

    // How far down to get back to: the description loads after the page opens, so the place is
    // reached as the page grows. Anything done to the scroll first wins.
    private double? _pendingOffset;

    private sealed record Spot(WorkshopItemTab Tab, double Offset);

    // The Comments tab's web view.
    private readonly SpModComments _commentsView;

    public WorkshopItemView()
    {
        InitializeComponent();

        _commentsView = new SpModComments(Scroll, CommentsHost, CommentsOff, CommentsFallback,
            () => _viewModel is { IsCommentsShown: true } shown ? shown.CommentsUrl : null);

        Scroll.ScrollChanged += (_, e) =>
        {
            if (e.ExtentHeightChange != 0) ReachPendingOffset();
        };
        Scroll.PreviewMouseWheel += (_, _) => _pendingOffset = null;
        Scroll.PreviewMouseDown += (_, _) => _pendingOffset = null;
        Scroll.PreviewKeyDown += (_, _) => _pendingOffset = null;
    }

    /// <summary>The item on screen, or hidden under another page; null once closed.</summary>
    public ModDetailsRequest? Current => _current;

    /// <summary>Raised when Esc walks back to the item this one was opened from.</summary>
    public event EventHandler? WalkedBack;

    /// <summary>Opens the item. <paramref name="again"/>: going back or forward to it, so it comes
    /// back at the tab and the place it was left at.</summary>
    public void Show(ModDetailsRequest request, bool again = false)
    {
        // Over the item on screen, Esc comes back to it; opened afresh (or again after being hidden
        // under another page), there is nothing to come back to.
        if (IsVisible && _current is not null) _history.Push(_current);
        else _history.Clear();

        Open(request, again);
    }

    /// <summary>Hides the open item while something opened from it is over it.</summary>
    public void Hide() => Visibility = Visibility.Collapsed;

    /// <summary>Shows the open item again, as it was.</summary>
    public void Reveal()
    {
        if (_current is null) return;

        Visibility = Visibility.Visible;
        Focus();
    }

    public void Close()
    {
        var wasOpen = _current is not null;
        KeepSpot();
        _pendingOffset = null;

        _history.Clear();
        _current = null;

        Detach();
        DataContext = null;
        Visibility = Visibility.Collapsed;
        HideComments();

        if (wasOpen) Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the page closes - the collection page opened under it comes back.</summary>
    public event EventHandler? Closed;

    private void Detach()
    {
        if (_viewModel is null) return;

        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.Detach();
        _viewModel = null;
    }

    private void Open(ModDetailsRequest request, bool again = false)
    {
        KeepSpot();
        _pendingOffset = null;
        Detach();

        _current = request;
        _viewModel = new WorkshopItemViewModel(request);
        _viewModel.PropertyChanged += OnViewModelChanged;
        DataContext = _viewModel;

        HideComments();
        Visibility = Visibility.Visible;
        Scroll.ScrollToTop();
        Strip.ScrollToLeftEnd();
        SyncPreviewStretch();

        // Focus so Esc reaches this rather than the page underneath.
        Focus();

        if (again && _spots.TryGetValue(request, out var spot))
        {
            if (spot.Tab != _viewModel.Tab) _viewModel.ReturnToTab(spot.Tab);
            _pendingOffset = spot.Offset > 0 ? spot.Offset : null;
            ReachPendingOffset();
        }

        _ = _viewModel.LoadAsync();
    }

    private void KeepSpot()
    {
        if (_current is null || _viewModel is null) return;

        // HUNCH: a hundred items is far more than anyone goes back through; past that, start over.
        if (_spots.Count >= 100) _spots.Clear();
        _spots[_current] = new Spot(_viewModel.Tab, Scroll.VerticalOffset);
    }

    private void ReachPendingOffset()
    {
        if (_pendingOffset is not { } offset) return;

        Scroll.ScrollToVerticalOffset(offset);
        if (Scroll.ScrollableHeight >= offset) _pendingOffset = null;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // A tab switch is a new page on Steam; start it at the top.
            case nameof(WorkshopItemViewModel.Tab):
                Scroll.ScrollToTop();
                if (_viewModel?.IsCommentsShown == true) _ = ShowCommentsAsync();
                else HideComments();
                break;

            case nameof(WorkshopItemViewModel.SelectedMedia):
                SyncPreviewStretch();
                break;
        }
    }

    private void GoBack()
    {
        if (_history.Count == 0)
        {
            Close();
            return;
        }

        Open(_history.Pop(), again: true);
        WalkedBack?.Invoke(this, EventArgs.Empty);
    }

    private void View_KeyDown(object sender, KeyEventArgs e)
    {
        var back = e.Key == Key.Escape || (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt) ||
                   (e.SystemKey == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt);
        if (!back) return;

        e.Handled = true;
        GoBack();
    }

    // The first two breadcrumbs lead back to the Workshop page this was opened from.
    private void Workshop_Click(object sender, RoutedEventArgs e) => Close();

    // ---------------------------------------------------------------- preview and strip

    // The mod's own picture and a video's thumbnail fill the preview, as before; a picture from the
    // description is never blown up past its own size (a badge would turn to mush).
    private void SyncPreviewStretch()
    {
        var media = _viewModel?.SelectedMedia;
        var first = _viewModel is not null && media is not null && ReferenceEquals(_viewModel.Gallery.FirstOrDefault(), media);
        PreviewPicture.StretchDirection = first || _viewModel?.SelectedIsVideo == true
            ? StretchDirection.Both
            : StretchDirection.DownOnly;
    }

    private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _viewModel?.OpenSelectedMediaCommand.Execute(null);
    }

    // The cover at the head of the right-hand column opens full size, as Steam's does.
    private void Cover_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _viewModel?.OpenCoverCommand.Execute(null);
    }

    // The slider under the strip mirrors the strip's own horizontal scroll, and drives it.
    private void Strip_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var overflow = Math.Max(0, Strip.ExtentWidth - Strip.ViewportWidth);
        StripSlider.Visibility = overflow > 0 ? Visibility.Visible : Visibility.Collapsed;
        StripSlider.Minimum = 0;
        StripSlider.Maximum = overflow;
        StripSlider.ViewportSize = Strip.ViewportWidth;
        StripSlider.Value = Strip.HorizontalOffset;
    }

    private void StripSlider_Scroll(object sender, ScrollEventArgs e) => Strip.ScrollToHorizontalOffset(e.NewValue);

    // ---------------------------------------------------------------- comments

    // sp-mod.com's comments for this item, in a web view - see SpModComments.
    private Task ShowCommentsAsync() => _commentsView.ShowAsync();

    private void HideComments() => _commentsView.Hide();

    private void OpenComments_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.CommentsUrl is { } url) MarkupActions.OpenInBrowser(url);
    }
}
