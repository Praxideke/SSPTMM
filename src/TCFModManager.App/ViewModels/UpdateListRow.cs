using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TCFModManager.App.Localization;

namespace TCFModManager.App.ViewModels;

public enum UpdateListRowState
{
    Ready,
    Waiting,
    Downloading,
    Installing,
    Done,
    Failed,
}

//
// Fork (1.4.3): one mod in a list of waiting updates (UpdateList, the arrow beside Update all).
// Its button updates that mod alone and then follows the download queue's card for it: waiting,
// the download's percentage, installing, a tick, or a cross to try again.
//
public sealed partial class UpdateListRow : ObservableObject
{
    public UpdateListRow(int modId) => ModId = modId;

    public int ModId { get; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _thumbnail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Versions))]
    private string? _fromVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Versions))]
    [NotifyPropertyChangedFor(nameof(UpdateToolTip))]
    private string? _toVersion;

    // Updated on top of what is there: said on the row, as the update asks about it.
    [ObservableProperty]
    private bool _isHandInstalled;

    [ObservableProperty]
    private bool _isDisabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(IsWorking))]
    [NotifyPropertyChangedFor(nameof(IsDone))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    [NotifyPropertyChangedFor(nameof(StateLabel))]
    [NotifyPropertyChangedFor(nameof(HasStateLabel))]
    private UpdateListRowState _state;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateLabel))]
    private double _progress;

    public string Versions => FromVersion is { } from
        ? LocalizationService.Text(Strings.Installed_UpdateListVersionsFormat, from, ToVersion)
        : ToVersion ?? string.Empty;

    public string UpdateToolTip => LocalizationService.Text(Strings.Installed_UpdateListUpdateFormat, ToVersion);

    public bool IsReady => State == UpdateListRowState.Ready;
    public bool IsWorking => State is UpdateListRowState.Waiting or UpdateListRowState.Downloading or UpdateListRowState.Installing;
    public bool IsDone => State == UpdateListRowState.Done;
    public bool IsFailed => State == UpdateListRowState.Failed;

    public bool HasStateLabel => State is not (UpdateListRowState.Ready or UpdateListRowState.Done);

    public string StateLabel => State switch
    {
        UpdateListRowState.Waiting => Strings.Installed_UpdateListWaiting,
        UpdateListRowState.Downloading => LocalizationService.Text(Strings.Installed_UpdateListPercentFormat, (int)Math.Round(Progress * 100)),
        UpdateListRowState.Installing => Strings.Installed_UpdateListInstalling,
        UpdateListRowState.Failed => Strings.Installed_UpdateListFailed,
        _ => string.Empty,
    };

    // The download queue's card for this mod's update, while there is one.
    public DownloadQueueItemViewModel? Item { get; private set; }

    public void Follow(DownloadQueueItemViewModel item)
    {
        if (ReferenceEquals(item, Item)) return;
        if (Item is not null) Item.PropertyChanged -= OnItemChanged;

        Item = item;
        item.PropertyChanged += OnItemChanged;
        Read();
    }

    public void Forget()
    {
        if (Item is not null) Item.PropertyChanged -= OnItemChanged;
        Item = null;
        State = UpdateListRowState.Ready;
        Progress = 0;
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DownloadQueueItemViewModel.Status) or nameof(DownloadQueueItemViewModel.Progress)) Read();
    }

    private void Read()
    {
        if (Item is not { } item) return;

        Progress = item.Progress;
        State = item.Status switch
        {
            DownloadQueueItemStatus.Pending => UpdateListRowState.Waiting,
            DownloadQueueItemStatus.Downloading => UpdateListRowState.Downloading,
            DownloadQueueItemStatus.Installing => UpdateListRowState.Installing,
            DownloadQueueItemStatus.Completed => UpdateListRowState.Done,
            DownloadQueueItemStatus.Failed => UpdateListRowState.Failed,
            _ => UpdateListRowState.Ready, // cancelled: it can be asked for again
        };
    }
}
