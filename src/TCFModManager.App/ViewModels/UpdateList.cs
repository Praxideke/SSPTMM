using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCFModManager.App.Localization;
using TCFModManager.App.Services;

namespace TCFModManager.App.ViewModels;

/// <summary>One mod with an update waiting, as the list shows it.</summary>
public readonly record struct UpdateListEntry(
    int ModId, string Title, string? Thumbnail, string? FromVersion, string? ToVersion, bool IsHandInstalled, bool IsDisabled);

//
// Fork (1.4.3): the list of waiting updates, from the arrow beside Update all or a right-click on
// it - each mod with its own update button, for updating some and not others. Subscribed items'
// and a collection page's each own one (UpdateListPanel shows it); the page says what is waiting
// and how to update and skip. A row follows its download queue card while the list is open,
// however the update was asked for (here, Update all, a card's own Update); rows that finished
// leave when the list closes.
//
public sealed partial class UpdateList : ObservableObject
{
    private readonly Action<IReadOnlyList<UpdateListRow>> _update;
    private readonly Func<UpdateListRow, Task>? _skip;
    private bool _followingQueue;

    /// <param name="update">Updates these rows' mods - asked and queued as that page's Update all does.</param>
    /// <param name="skip">Skips a row's update; null where the page can't, and the rows offer no Skip.</param>
    public UpdateList(Action<IReadOnlyList<UpdateListRow>> update, Func<UpdateListRow, Task>? skip = null)
    {
        _update = update;
        _skip = skip;
    }

    public ObservableCollection<UpdateListRow> Rows { get; } = [];

    public bool IsOpen { get; private set; }

    public bool CanSkip => _skip is not null;

    /// <summary>After a row's update or the list's Update all - a question it asked closes the list,
    /// so the page opens it again.</summary>
    public event EventHandler? Acted;

    /// <summary>The list opened, closed, or a row changed state.</summary>
    public event EventHandler? Changed;

    /// <summary>The rows that can still be updated - the count on the list's own Update all.</summary>
    public int ReadyCount => Rows.Count(r => r.IsReady || r.IsFailed);

    public bool HasReady => ReadyCount > 0;

    public string AllLabel => Strings.Installed_UpdateAll(ReadyCount);

    /// <summary>Fills the list with what is waiting, keeping the rows already updating.</summary>
    public void Open(IReadOnlyList<UpdateListEntry> waiting)
    {
        if (!_followingQueue)
        {
            AppServices.DownloadQueue.Items.CollectionChanged += OnQueueItemsChanged;
            _followingQueue = true;
        }

        IsOpen = true;

        foreach (var stale in Rows.Where(r => !r.IsWorking && !r.IsDone && waiting.All(w => w.ModId != r.ModId)).ToList())
            Rows.Remove(stale);

        foreach (var entry in waiting)
        {
            var row = Rows.FirstOrDefault(r => r.ModId == entry.ModId);
            if (row is null)
            {
                row = new UpdateListRow(entry.ModId);
                row.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(UpdateListRow.State)) Announce();
                };
                Rows.Add(row);
            }

            row.Title = entry.Title;
            row.Thumbnail = entry.Thumbnail;
            row.FromVersion = entry.FromVersion;
            row.ToVersion = entry.ToVersion;
            row.IsHandInstalled = entry.IsHandInstalled;
            row.IsDisabled = entry.IsDisabled;

            if (QueuedFor(entry.ModId, row.ToVersion) is { } item) row.Follow(item);
        }

        Announce();
    }

    /// <summary>The list closed: what finished leaves it, so it opens on what is left.</summary>
    public void Close()
    {
        foreach (var done in Rows.Where(r => r.IsDone).ToList())
        {
            done.Forget();
            Rows.Remove(done);
        }

        IsOpen = false;
        Announce();
    }

    [RelayCommand]
    private void One(UpdateListRow? row)
    {
        if (row is null || row.IsWorking || row.IsDone) return;

        // A failed card goes round again rather than queuing a second one beside it.
        if (row is { IsFailed: true, Item: { CanRetry: true } failed })
        {
            failed.RetryCommand.Execute(null);
            return;
        }

        row.Forget();
        _update([row]);
        Acted?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void All()
    {
        var rows = Rows.Where(r => r.IsReady || r.IsFailed).ToList();
        if (rows.Count == 0) return;
        _update(rows);
        Acted?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task SkipAsync(UpdateListRow? row)
    {
        if (_skip is null || row is not { IsReady: true, ToVersion: not null }) return;

        Rows.Remove(row);
        Announce();
        await _skip(row);
    }

    private void OnQueueItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null) return;

        foreach (var item in e.NewItems.OfType<DownloadQueueItemViewModel>())
        {
            if (item.IsAddon) continue;
            Rows.FirstOrDefault(r => r.ModId == item.Target.Id)?.Follow(item);
        }
    }

    /// <summary>Stops following the queue - the page that owned the list is gone.</summary>
    public void Detach()
    {
        if (!_followingQueue) return;
        AppServices.DownloadQueue.Items.CollectionChanged -= OnQueueItemsChanged;
        _followingQueue = false;
        foreach (var row in Rows) row.Forget();
        Rows.Clear();
    }

    // The newest queue card for this update - this mod at this version - unless it was cancelled.
    private static DownloadQueueItemViewModel? QueuedFor(int modId, string? version) =>
        AppServices.DownloadQueue.Items.LastOrDefault(i =>
            !i.IsAddon && i.Target.Id == modId && i.VersionLabel == version && i.Status != DownloadQueueItemStatus.Cancelled);

    private void Announce()
    {
        OnPropertyChanged(nameof(ReadyCount));
        OnPropertyChanged(nameof(HasReady));
        OnPropertyChanged(nameof(AllLabel));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
