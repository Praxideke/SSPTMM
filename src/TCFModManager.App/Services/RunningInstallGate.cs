using System.Windows;
using TCFModManager.App.Localization;
using TCFModManager.App.Views;
using TCFModManager.Core.Services;

namespace TCFModManager.App.Services;

//
// Fork (1.4.3): Options > Allow changes while SPT is running. With it on, a change the running SPT
// server or game would have refused asks first, as Mod Organizer 2 does, and goes ahead on a yes.
// Restoring profiles is still refused: the server writes the profiles back on its way out.
//
internal static class RunningInstallGate
{
    // A yes holds this long after the last change it let through, while the same programs run, so a
    // batch - several mods removed, a list applied, the second check inside each - asks once. HUNCH.
    private static readonly TimeSpan Remembered = TimeSpan.FromMinutes(2);

    private static readonly object Sync = new();
    private static string? _approvedFor;
    private static DateTime _approvedUntil;

    public static bool Enabled { get; set; }

    public static void Register() => ModInstallService.ConfirmInUse = Confirm;

    private static bool Confirm(ModInstallAction action, IReadOnlyList<string> running)
    {
        if (!Enabled || action == ModInstallAction.RestoreProfiles) return false;

        var key = string.Join("|", running);
        lock (Sync)
        {
            if (_approvedFor == key && DateTime.UtcNow < _approvedUntil)
            {
                _approvedUntil = DateTime.UtcNow + Remembered;
                return true;
            }
        }

        if (Application.Current?.Dispatcher is not { } dispatcher) return false;

        var yes = dispatcher.Invoke(() => SteamDialog.Show(
            Strings.InUse_ConfirmTitle,
            LocalizationService.Text(Strings.InUse_ConfirmFormat, TextLists.Join(running)),
            new SteamDialogChoice(Strings.InUse_GoAhead, SteamDialogButton.Blue),
            new SteamDialogChoice(Strings.Common_Cancel, SteamDialogButton.Grey, IsDefault: true)) == 0);

        if (yes)
        {
            lock (Sync)
            {
                _approvedFor = key;
                _approvedUntil = DateTime.UtcNow + Remembered;
            }
        }

        AppLog.Info("Install", (yes ? "went ahead while running: " : "stopped, running: ") + key + " (" + action + ")");
        return yes;
    }
}
