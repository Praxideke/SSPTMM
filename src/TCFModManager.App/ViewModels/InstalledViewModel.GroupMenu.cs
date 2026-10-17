using System.Windows;
using System.Windows.Controls;
using TCFModManager.App.Localization;
using TCFModManager.App.Services;
using TCFModManager.App.Views;

namespace TCFModManager.App.ViewModels;

//
// Fork (SSPTMM 1.4.2): putting several mods in a group at once - dragging one of several picked
// mods carries them all, and the right-click menu's "Add to group" (ModContextMenu) does the same
// without dragging, with a new group named on the spot.
//
public partial class InstalledViewModel
{
    /// <summary>The mods an action on this one stands for: every picked mod when it is one of two or
    /// more picked, as in Steam's library; otherwise itself alone.</summary>
    public IReadOnlyList<InstalledModCardViewModel> ModsFor(InstalledModCardViewModel mod)
    {
        if (!mod.IsSelected) return [mod];

        var picked = SelectedCards();
        return picked.Count > 1 ? picked : [mod];
    }

    /// <summary>Your groups by name, as the Group filter lists them.</summary>
    public IReadOnlyList<(Guid Id, string Name)> GroupChoices() =>
        AppServices.ModGroups.Load().Groups
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Id, g.Name))
            .ToList();

    /// <summary>Puts every one of these mods in the group (null: back to Ungrouped).</summary>
    public void MoveModsToGroup(IReadOnlyList<InstalledModCardViewModel> mods, Guid? groupId)
    {
        if (mods.Count == 0) return;

        AppServices.ModGroups.AssignMods(mods.Select(m => m.Name), groupId);
        GroupsChanged();
    }

    /// <summary>Asks for a name, makes the group and puts the mods in it. Nothing when cancelled.</summary>
    public void MoveModsToNewGroup(IReadOnlyList<InstalledModCardViewModel> mods)
    {
        if (AskGroupName() is not { } name) return;

        var group = AppServices.ModGroups.AddGroup(name);
        MoveModsToGroup(mods, group.Id);
    }

    // Fork (1.4.3): a name of your own for a mod, shown wherever its title is. Empty gives it back
    // its own.
    public void RenameMod(InstalledModCardViewModel card)
    {
        var own = card.MatchedModName ?? card.DeclaredName ?? card.Name;
        var box = new TextBox
        {
            Style = (Style)Application.Current.FindResource("SteamSearchField"),
            Tag = own,
            Text = card.CustomName ?? string.Empty,
            Height = 34,
            MinWidth = 420,
        };
        box.Loaded += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };

        var answer = SteamDialog.Show(
            Strings.Menu_RenameTitle,
            box,
            new SteamDialogChoice(Strings.Menu_RenameSave, SteamDialogButton.Green, IsDefault: true),
            new SteamDialogChoice(Strings.Common_Cancel, SteamDialogButton.Grey));
        if (answer != 0) return;

        var name = box.Text.Trim();
        ModNicknames.Set(card, name.Length == 0 || name == own ? null : name);
        ApplyFilter();
    }

    // Null when cancelled or left blank.
    private static string? AskGroupName()
    {
        var box = new TextBox
        {
            Style = (Style)Application.Current.FindResource("SteamSearchField"),
            Tag = Strings.Installed_NewGroupPlaceholder,
            Height = 34,
            MinWidth = 420,
        };
        box.Loaded += (_, _) => box.Focus();

        var answer = SteamDialog.Show(
            Strings.Menu_NewGroupTitle,
            box,
            new SteamDialogChoice(Strings.Installed_AddGroup, SteamDialogButton.Green, IsDefault: true),
            new SteamDialogChoice(Strings.Common_Cancel, SteamDialogButton.Grey));

        var name = box.Text.Trim();
        return answer == 0 && name.Length > 0 ? name : null;
    }
}
