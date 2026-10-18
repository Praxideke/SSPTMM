using System.Windows;
using TCFModManager.App.Localization;
using TCFModManager.App.Views;
using TCFModManager.Core.Services;

namespace TCFModManager.App.Services;

//
// Fork (1.4.2): turns on SPT's own cleaning of what's missing from a profile (SptProfileFixes),
// after saying what it costs - the items and traders it removes don't come back if the mod is put
// back later. Asked from Diagnose logs and from Play's "SPT won't load this profile".
//
internal static class SptProfileFixPrompt
{
    /// <summary>Done is true once both switches are on. Problem is the line to show when it
    /// couldn't be done; both are false/null when it was cancelled.</summary>
    public static (bool Done, string? Problem) Run(string configPath)
    {
        var answer = SteamMessageBox.Show(
            LocalizationService.Text(Strings.Diagnose_ProfileFixConfirmFormat, configPath, SptProfileFixes.BackupSuffix),
            Strings.Diagnose_ProfileFixTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return (false, null);

        return SptProfileFixes.TurnOn(configPath) switch
        {
            null => (true, null),
            "" => (false, Strings.Diagnose_ProfileFixNotFound),
            var failed => (false, LocalizationService.Text(Strings.Diagnose_ProfileFixFailedFormat, failed)),
        };
    }
}
