using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using TCFModManager.App.Localization;
using TCFModManager.App.Services;
using TCFModManager.App.Views;
using TCFModManager.Core.Services;

namespace TCFModManager.App.ViewModels;

//
// Fork (SSPTMM 1.4.2): colours for mods and groups on Subscribed items. A group's colour runs
// down the edge of each of its mods' cards and rows, and a mod given one of its own shows that
// instead. The Colour filter and "color:red" in the search box find them again.
//
public partial class InstalledViewModel
{
    public ObservableCollection<ColorFilterItem> ColorFilterOptions { get; } = [ColorFilterItem.Any, ColorFilterItem.None];

    [ObservableProperty]
    private ColorFilterItem _selectedColorFilter = ColorFilterItem.Any;

    partial void OnSelectedColorFilterChanged(ColorFilterItem value) => AutoApplyFilter();

    /// <summary>Gives the mods a colour of their own, or (null) takes theirs away.</summary>
    public void ColorMods(IReadOnlyList<InstalledModCardViewModel> mods, string? color)
    {
        if (mods.Count == 0) return;

        ModOwnColors.Set(mods, color);
        RefreshColorFilter();
        ApplyFilter();
    }

    /// <summary>Gives the group a colour, or (null) takes it away.</summary>
    public void ColorGroup(ModGroupSectionViewModel section, string? color)
    {
        if (section.GroupId is not { } id) return;

        AppServices.ModGroups.SetColor(id, color);
        GroupsChanged();
    }

    // The colours in use, the palette's first in its order and then any of your own, after Any
    // colour and No colour. A colour picked that is no longer in use goes back to Any colour.
    private void RefreshColorFilter()
    {
        var used = _all.Select(m => m.Color).OfType<string>().Distinct().ToList();
        var ordered = ModColors.Palette.Select(s => s.Hex).Where(used.Contains)
            .Concat(used.Where(c => ModColors.SwatchOf(c) is null).Order(StringComparer.Ordinal));

        var previous = SelectedColorFilter;
        _suppressAutoApplyFilter = true;
        try
        {
            ColorFilterOptions.Clear();
            ColorFilterOptions.Add(ColorFilterItem.Any);
            ColorFilterOptions.Add(ColorFilterItem.None);
            foreach (var color in ordered) ColorFilterOptions.Add(ColorFilterItem.Of(color));

            SelectedColorFilter = ColorFilterOptions.FirstOrDefault(o => o.SameAs(previous)) ?? ColorFilterItem.Any;
        }
        finally
        {
            _suppressAutoApplyFilter = false;
        }
    }

    // What follows "color:" (or "colour:", or the word in the app's language) in the search box;
    // null when the search is not for a colour.
    private static string? ColorQuery(string query)
    {
        foreach (var word in new[] { "color:", "colour:", Strings.Installed_ColorSearchWord + ":" })
        {
            if (query.StartsWith(word, StringComparison.CurrentCultureIgnoreCase)) return query[word.Length..].Trim();
        }

        return null;
    }

    /// <summary>Six hex digits typed in, for a colour the palette doesn't have. Null when cancelled.</summary>
    public static string? AskCustomColor(string? current)
    {
        var text = current ?? string.Empty;
        var wrong = false;
        while (true)
        {
            // Made again for each try: a dialog's content can't be handed on to the next one.
            var box = new TextBox
            {
                Style = (Style)Application.Current.FindResource("SteamSearchField"),
                Tag = Strings.Menu_ColorCustomHint,
                Text = text,
                Height = 34,
                MinWidth = 260,
                MaxLength = 9,
            };
            box.Loaded += (_, _) =>
            {
                box.Focus();
                box.SelectAll();
            };
            var invalid = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                Text = Strings.Options_AccentInvalid,
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("SystemFillColorCautionBrush"),
                Visibility = wrong ? Visibility.Visible : Visibility.Collapsed,
            };

            var answer = SteamDialog.Show(
                Strings.Menu_ColorCustomTitle,
                new StackPanel { Children = { box, invalid } },
                new SteamDialogChoice(Strings.Common_Save, SteamDialogButton.Green, IsDefault: true),
                new SteamDialogChoice(Strings.Common_Cancel, SteamDialogButton.Grey));
            if (answer != 0) return null;
            if (ModColors.Normalize(box.Text) is { } color) return color;

            text = box.Text;
            wrong = true;
        }
    }
}
