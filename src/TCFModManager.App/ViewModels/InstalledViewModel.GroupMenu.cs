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
