using System.Diagnostics;
using System.Text;
using System.Windows;
using TCFModManager.App.Localization;
using TCFModManager.Core.Services;

namespace TCFModManager.App.Services;

//
// Fork (1.4.3): what a failure is copied or reported as - the app's, SPT's and Windows' versions,
// what went wrong, and the error behind it - with the user's name and profile folder taken out
// (LogRedactor), as the Diagnose report is. A GitHub issue opens with it filled in; nothing is sent
// until the user presses Submit there.
//
public static class ErrorReport
{
    // How much of the report goes into the new issue's address: GitHub turns away addresses much
    // past 8,000 characters, and escaping can triple the text. The whole is on the clipboard. HUNCH.
    private const int IssueBodyLimit = 2500;

    public static string Versions() =>
        LocalizationService.Text(
            Strings.Report_VersionsFormat,
            SelfMod.ShortName + " " + AppVersion.Current,
            AppServices.SptEnvironment.InstalledVersion ?? "?",
            WindowsVersion());

    // Windows 11 still reports itself as 10.0; its builds start at 22000.
    public static string WindowsVersion()
    {
        var version = Environment.OSVersion.Version;
        var major = version is { Major: 10, Build: >= 22000 } ? 11 : version.Major;
        return LocalizationService.Text(Strings.Report_WindowsFormat, major, version);
    }

    public static string Build(string what, Exception? error = null)
    {
        var text = new StringBuilder();
        text.AppendLine(Versions());
        text.AppendLine();
        text.AppendLine(what);
        if (error is not null)
        {
            text.AppendLine();
            text.AppendLine(error.ToString());
        }

        return Redact(text.ToString());
    }

    public static string Redact(string text) => LogRedactor.Redact(
        text,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.UserName);

    // True when it is on the clipboard; the clipboard can be held by another program for a moment.
    public static bool Copy(string report)
    {
        try
        {
            Clipboard.SetText(report);
            return true;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            AppLog.Warn("Report", "couldn't copy: " + ex.Message);
            return false;
        }
    }

    // A new issue on the project's GitHub, its title and text filled in, in the browser.
    public static void OpenIssue(string title, string report)
    {
        Copy(report);

        var body = report.Length <= IssueBodyLimit
            ? report
            : report[..IssueBodyLimit] + Environment.NewLine + Strings.Report_Shortened;
        var url = SelfMod.IssuesUrl + "/new?title=" + Uri.EscapeDataString(title)
            + "&body=" + Uri.EscapeDataString("```" + Environment.NewLine + body + Environment.NewLine + "```");

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            AppLog.Warn("Report", "couldn't open the issue page: " + ex.Message);
        }
    }

    // The issue's title: the error's first line, kept short.
    public static string Title(string what)
    {
        var line = what.Split('\n')[0].Trim();
        return line.Length <= 90 ? line : line[..87] + "...";
    }
}
