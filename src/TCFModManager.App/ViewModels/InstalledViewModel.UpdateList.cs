using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using TCFModManager.App.Localization;
using TCFModManager.App.Services;

namespace TCFModManager.App.ViewModels;

//
// Fork (1.4.2): the list of waiting updates, from the arrow beside Update all or a right-click on
// it - each mod with its own update button, for updating some and not others. A row follows its
// download queue card while the list is open, however the update was asked for (here, Update all,
// a card's own Update); rows that finished leave when the list closes.
//
public partial class InstalledViewModel
{
    public ObservableCollection<UpdateListRow> UpdateRows { get; } = [];

    private bool _followingQueue;
    private bool _updateListOpen;

    // Update all stays while its list is open, though the last update has just finished.
    public bool ShowsUpdateAll => HasUpdates || _updateListOpen;

    /// <summary>After a row's update or the list's Update all - a question it asked closes the list,
    /// so the page opens it again.</summary>
    public event EventHandler? UpdateListActed;

    /// <summary>The rows that can still be updated - the count on the list's own Update all.</summary>
    public int UpdateListReadyCount => UpdateRows.Count(r => r.IsReady || r.IsFailed);

    public bool HasUpdateListReady => UpdateListReadyCount > 0;

    public string UpdateListAllLabel => Strings.Installed_UpdateAll(UpdateListReadyCount);

    /// <summary>Fills the list from the mods with an update, keeping the rows already updating.</summary>
    public void OpenUpdateList()
    {
        if (!_followingQueue)
        {
            AppServices.DownloadQueue.Items.CollectionChanged += OnQueueItemsChanged;
            _followingQueue = true;
        }

        _updateListOpen = true;

        var updatable = _all.Where(IsUpdatable).ToList();

        foreach (var stale in UpdateRows.Where(r => !r.IsWorking && !r.IsDone && updatable.All(c => c.ModId != r.ModId)).ToList())
            UpdateRows.Remove(stale);

        foreach (var card in updatable)
        {
            var id = card.ModId!.Value;
            var row = UpdateRows.FirstOrDefault(r => r.ModId == id);
            if (row is null)
            {
                row = new UpdateListRow(id);
                row.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(UpdateListRow.State)) AnnounceUpdateList();
                };
                UpdateRows.Add(row);
            }

            row.Title = card.DisplayTitle;
            row.Thumbnail = card.Thumbnail;
            row.FromVersion = card.InstalledVersion;
            row.ToVersion = card.UpdateVersion ?? card.LatestPublishedVersion;
            row.IsHandInstalled = !card.IsAppManaged;
            row.IsDisabled = card.IsDisabled;

            if (QueuedFor(id, row.ToVersion) is { } item) row.Follow(item);
        }

        AnnounceUpdateList();
    }

    /// <summary>The list closed: what finished leaves it, so it opens on what is left.</summary>
    public void CloseUpdateList()
    {
        foreach (var done in UpdateRows.Where(r => r.IsDone).ToList())
        {
            done.Forget();
            UpdateRows.Remove(done);
        }

        _updateListOpen = false;
        AnnounceUpdateList();
    }

    [RelayCommand]
    private void UpdateListOne(UpdateListRow? row)
    {
        if (row is null || row.IsWorking || row.IsDone) return;

        // A failed card goes round again rather than queuing a second one beside it.
        if (row is { IsFailed: true, Item: { CanRetry: true } failed })
        {
            failed.RetryCommand.Execute(null);
            return;
        }

        if (_all.FirstOrDefault(c => c.ModId == row.ModId && !c.IsAddon) is not { } card) return;
        row.Forget();
        UpdateCards([card]);
        UpdateListActed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void UpdateListAll()
    {
        var cards = UpdateRows
            .Where(r => r.IsReady || r.IsFailed)
            .Select(r => _all.FirstOrDefault(c => c.ModId == r.ModId && !c.IsAddon))
            .OfType<InstalledModCardViewModel>()
            .ToList();
        if (cards.Count == 0) return;
        UpdateCards(cards);
        UpdateListActed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task UpdateListSkipAsync(UpdateListRow? row)
    {
        if (row is not { IsReady: true, ToVersion: { } version }
            || _all.FirstOrDefault(c => c.ModId == row.ModId && !c.IsAddon) is not { } card)
        {
            return;
        }

        UpdateRows.Remove(row);
        AnnounceUpdateList();
        await SkipUpdateAsync(card, version);
    }

    private void OnQueueItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null) return;

        foreach (var item in e.NewItems.OfType<DownloadQueueItemViewModel>())
        {
            if (item.IsAddon) continue;
            UpdateRows.FirstOrDefault(r => r.ModId == item.Target.Id)?.Follow(item);
        }
    }

    // The newest queue card for this update - this mod at this version - unless it was cancelled.
    private static DownloadQueueItemViewModel? QueuedFor(int modId, string? version) =>
        AppServices.DownloadQueue.Items.LastOrDefault(i =>
            !i.IsAddon && i.Target.Id == modId && i.VersionLabel == version && i.Status != DownloadQueueItemStatus.Cancelled);

    private void AnnounceUpdateList()
    {
        OnPropertyChanged(nameof(UpdateListReadyCount));
        OnPropertyChanged(nameof(HasUpdateListReady));
        OnPropertyChanged(nameof(ShowsUpdateAll));
        OnPropertyChanged(nameof(UpdateListAllLabel));
    }
}
