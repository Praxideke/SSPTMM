namespace TCFModManager.App.ViewModels;

//
// Fork (1.4.3): Subscribed items' list of waiting updates (UpdateList), over every mod with one.
//
public partial class InstalledViewModel
{
    private UpdateList? _updateList;

    public UpdateList UpdateList => _updateList ??= CreateUpdateList();

    // Update all stays while its list is open, though the last update has just finished.
    public bool ShowsUpdateAll => HasUpdates || (_updateList?.IsOpen ?? false);

    private UpdateList CreateUpdateList()
    {
        var list = new UpdateList(rows => UpdateCards(CardsFor(rows)), SkipRowAsync);
        list.Changed += (_, _) => OnPropertyChanged(nameof(ShowsUpdateAll));
        return list;
    }

    /// <summary>Fills the list from the mods with an update, keeping the rows already updating.</summary>
    public void OpenUpdateList() =>
        UpdateList.Open([.. _all.Where(IsUpdatable).Select(card => new UpdateListEntry(
            card.ModId!.Value,
            card.DisplayTitle,
            card.Thumbnail,
            card.InstalledVersion,
            card.UpdateVersion ?? card.LatestPublishedVersion,
            IsHandInstalled: !card.IsAppManaged,
            card.IsDisabled))]);

    public void CloseUpdateList() => UpdateList.Close();

    private List<InstalledModCardViewModel> CardsFor(IReadOnlyList<UpdateListRow> rows) =>
        [.. rows
            .Select(r => _all.FirstOrDefault(c => c.ModId == r.ModId && !c.IsAddon))
            .OfType<InstalledModCardViewModel>()];

    private async Task SkipRowAsync(UpdateListRow row)
    {
        if (_all.FirstOrDefault(c => c.ModId == row.ModId && !c.IsAddon) is { } card)
            await SkipUpdateAsync(card, row.ToVersion);
    }
}
