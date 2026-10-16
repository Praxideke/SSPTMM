using System.Windows;
using System.Windows.Controls;
using TCFModManager.App.Localization;
using TCFModManager.App.Views;
using TCFModManager.Core.Services;

namespace TCFModManager.App.Services;

//
// Fork (1.4.2): an archive with none of the folders a mod's archive has. Its files are shown and the
// user says whether it is a client or a server mod - the likelier one first - or cancels, which
// refuses it as before. Cancel is the default: a wrong guess puts files where nothing loads them.
//
internal static class ArchiveLayoutQuestion
{
    public static ArchivePlacement? Ask(UnrecognisedArchive archive)
    {
        if (Application.Current?.Dispatcher is not { } dispatcher) return null;

        return dispatcher.Invoke(() =>
        {
            var body = new StackPanel();
            body.Children.Add(Line(LocalizationService.Text(Strings.Install_LayoutBodyFormat, archive.ModName, archive.Version)));

            var listing = string.Join("\n", archive.Files);
            if (archive.FileCount > archive.Files.Count)
                listing += "\n" + LocalizationService.Text(Strings.Install_LayoutMoreFormat, archive.FileCount - archive.Files.Count);
            body.Children.Add(new ScrollViewer
            {
                MaxHeight = 220,
                Margin = new Thickness(0, 8, 0, 8),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new TextBlock { Text = listing, FontFamily = new System.Windows.Media.FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap },
            });
            body.Children.Add(Line(Strings.Install_LayoutAdvice));

            var client = new SteamDialogChoice(Strings.Install_LayoutClient,
                archive.Likely == ArchivePlacement.ClientPlugin ? SteamDialogButton.Blue : SteamDialogButton.Grey);
            var server = new SteamDialogChoice(Strings.Install_LayoutServer,
                archive.Likely == ArchivePlacement.ServerMod ? SteamDialogButton.Blue : SteamDialogButton.Grey);
            var serverFirst = archive.Likely == ArchivePlacement.ServerMod;

            var choice = SteamDialog.Show(
                LocalizationService.Text(Strings.Install_LayoutTitleFormat, archive.ModName),
                body,
                serverFirst ? server : client,
                serverFirst ? client : server,
                new SteamDialogChoice(Strings.Common_Cancel, SteamDialogButton.Grey, IsDefault: true));

            return choice switch
            {
                0 => serverFirst ? ArchivePlacement.ServerMod : ArchivePlacement.ClientPlugin,
                1 => serverFirst ? ArchivePlacement.ClientPlugin : ArchivePlacement.ServerMod,
                _ => (ArchivePlacement?)null,
            };
        });
    }

    private static TextBlock Line(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
}
