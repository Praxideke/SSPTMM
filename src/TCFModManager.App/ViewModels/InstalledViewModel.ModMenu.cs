using System.IO;
using System.Windows;
using System.Windows.Controls;
using TCFModManager.App.Localization;
using TCFModManager.App.Services;
using TCFModManager.App.Views;
using TCFModManager.Core.Services;

namespace TCFModManager.App.ViewModels;

//
// Fork (SSPTMM 1.4.3): what a mod's right-click menu on Subscribed items adds for that one mod -
// skipping an update, going back to the version an update replaced, and a note of your own.
//
public partial class InstalledViewModel
{
    /// <summary>The version an update of this mod would install, when it can be skipped.</summary>
    public static string? SkippableUpdate(InstalledModCardViewModel card) =>
        card is { UpdateAvailable: true, ModId: not null }
            ? card.UpdateVersion ?? card.LatestPublishedVersion
            : null;

    /// <summary>Skips this update, or (null) stops skipping, then scans so every count agrees.</summary>
    public async Task SkipUpdateAsync(InstalledModCardViewModel card, string? version)
    {
        if (card.ModId is not { } id) return;

        var title = card.DisplayTitle;
        SkippedUpdates.Set(id, card.IsAddon, version);

        await ScanAsync();
        ModsMoved?.Invoke(this, EventArgs.Empty); // Browse and the item pages offer Update by the same rule

        StatusMessage = version is null
            ? Text(Strings.Installed_StoppedSkippingFormat, title)
            : Text(Strings.Installed_SkippingFormat, title, version);
    }

    /// <summary>The version an update replaced that is still held, so it can be gone back to.</summary>
    public static string? GoBackVersion(InstalledModCardViewModel card)
    {
        var installPath = AppServices.SptEnvironment.InstallPath;
        if (string.IsNullOrWhiteSpace(installPath)
            || card is not { IsAppManaged: true, ModId: { } id, InstalledVersion: { } current }
            || card.Entries.Any(e => e.IsDisabled)) // its files aren't where the record says; switch it on first
        {
            return null;
        }

        return RemovedMods.LatestReplaced(installPath, id, card.IsAddon, current)?.Log.Record?.Version;
    }

    //
    // Puts back the version the last update of this mod replaced, from the copy held when it was
    // updated (ModInstallService.RevertUpdate) - offline, and after the author has taken it down.
    // The version it replaces is held the same way, so the menu then offers that one back.
    //
    public async Task GoBackAsync(InstalledModCardViewModel card)
    {
        var installPath = AppServices.SptEnvironment.InstallPath;
        if (string.IsNullOrWhiteSpace(installPath) || card.ModId is not { } id || card.InstalledVersion is not { } current) return;

        var held = RemovedMods.LatestReplaced(installPath, id, card.IsAddon, current);
        var record = AppServices.InstallManifest.Load().Find(id, card.IsAddon);
        if (held is not { } found || found.Log.Record is not { } old || record is null)
        {
            StatusMessage = Strings.Installed_UndoNothing;
            return;
        }

        var title = card.DisplayTitle;
        var answer = SteamDialog.Show(
            Text(Strings.Installed_GoBackTitleFormat, old.Version),
            Paragraph(Text(Strings.Installed_GoBackBodyFormat, title, old.Version, current)),
            new SteamDialogChoice(Strings.Installed_GoBack, SteamDialogButton.Green, IsDefault: true),
            new SteamDialogChoice(Strings.Common_Cancel, SteamDialogButton.Grey));
        if (answer != 0) return;

        // The queue moves the same files and writes the same records.
        if (InstallingNow() is { } busy)
        {
            StatusMessage = Text(Strings.Installed_WaitForInstallFormat, busy);
            return;
        }

        string message;
        IsBusy = true;
        try
        {
            // Off the UI thread: a large mod is a lot of files to move.
            var result = await Task.Run(() => AppServices.ModInstall.RevertUpdate(installPath, record, found.Folder));

            message = !result.Ran
                ? Strings.Installed_UndoNothing
                : result.Blocked.Count == 0
                    ? Text(Strings.Installed_WentBackFormat, title, old.Version)
                    : Text(Strings.Installed_WentBackBlockedFormat, title, old.Version, found.Folder);

            ModRemoved?.Invoke(this, EventArgs.Empty);
        }
        catch (ModInstallException ex)
        {
            message = ModInstallProblems.Describe(ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            message = Text(Strings.Installed_RemoveFailedFormat, title, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        await ScanAsync();
        StatusMessage = message;
    }

    /// <summary>A line of your own on the mod's card; empty removes it.</summary>
    public void EditNote(InstalledModCardViewModel card)
    {
        var box = new TextBox
        {
            Style = (Style)Application.Current.FindResource("SteamSearchField"),
            Tag = Strings.Menu_NoteHint,
            Text = card.Note ?? string.Empty,
            Height = 34,
            MinWidth = 480,
            MaxLength = 300, // HUNCH: a line or two on a card, not a document
        };
        box.Loaded += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };

        var answer = SteamDialog.Show(
            Text(Strings.Menu_NoteTitleFormat, card.DisplayTitle),
            box,
            new SteamDialogChoice(Strings.Common_Save, SteamDialogButton.Green, IsDefault: true),
            new SteamDialogChoice(Strings.Common_Cancel, SteamDialogButton.Grey));
        if (answer != 0) return;

        var note = box.Text.Trim();
        ModNotes.Set(card, note.Length == 0 ? null : note);
        ApplyFilter();
    }
}
