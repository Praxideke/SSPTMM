using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCFModManager.App.Localization;
using TCFModManager.App.Services;
using TCFModManager.Core.Models;
using TCFModManager.Core.Services;

namespace TCFModManager.App.ViewModels;

//
// Fork (1.4.2, experimental): Options > Play with Fika. On - or Automatic, with Fika found in the
// install (FikaInstall) - the Play card offers other PCs' servers (PlayViewModel.Servers), and:
//   - a Fika host is told what to give friends: this PC's addresses with the port from fika.jsonc,
//     which Fika listens on in place of http.json's, and the two ports Fika's wiki says to forward
//     (TCP for the server, UDP 25565 for whoever hosts the raid);
//   - a player on another PC's server is told when this PC's own server is running too: with Fika
//     only the host runs one, and Fika's FAQ names a stray one as a cause of friends not seeing
//     each other.
//
public partial class PlayViewModel
{
    [ObservableProperty]
    private bool _otherServersOn;

    [ObservableProperty]
    private string? _fikaHostNote;

    // The first of the addresses, for Copy address.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyFikaAddressCommand))]
    private string? _fikaHostAddress;

    // Fika's settings say something that keeps friends out, or are not written yet.
    [ObservableProperty]
    private string? _fikaHostWarning;

    [ObservableProperty]
    private string? _fikaStrayServerNote;

    private FikaInstall.Found _fikaFound;
    private string? _fikaFoundFor;
    private bool _lookingForFika;

    // This PC's addresses and Fika's settings, read again every so often rather than every 2s poll.
    // 15s is HUNCH: a changed fika.jsonc needs a server restart anyway.
    private (string? Root, DateTime At, IReadOnlyList<string> Addresses, FikaServerConfig? Config)? _fikaHostRead;

    // From RefreshDirect, before the servers are listed: whether they are offered at all.
    private void RefreshFika(string? installPath, AppSettings settings)
    {
        if (FikaInstall.Known(installPath) is { } known)
        {
            _fikaFound = known;
            _fikaFoundFor = installPath;
        }
        else
        {
            // Kept while it is read again, so a picked server doesn't drop out every few minutes.
            if (!string.Equals(_fikaFoundFor, installPath, StringComparison.OrdinalIgnoreCase)) _fikaFound = default;
            if (!string.IsNullOrWhiteSpace(installPath) && !_lookingForFika) _ = LookForFikaAsync(installPath);
        }

        OtherServersOn = settings.FikaOptions ?? _fikaFound.Any;
    }

    private async Task LookForFikaAsync(string installPath)
    {
        _lookingForFika = true;
        try
        {
            var found = await FikaInstall.ReadAsync(installPath);
            var changed = found != _fikaFound || !string.Equals(_fikaFoundFor, installPath, StringComparison.OrdinalIgnoreCase);
            _fikaFound = found;
            _fikaFoundFor = installPath;
            if (!changed) return;

            AppLog.Info("Fika", $"in the install: plugin {(found.Client ? "yes" : "no")}, server mod {(found.Server ? "yes" : "no")}");
            Refresh();
        }
        finally
        {
            _lookingForFika = false;
        }
    }

    // From RefreshDirect, once the server picked is known.
    private void RefreshFikaNotes(string? installPath)
    {
        FikaStrayServerNote = OtherServersOn && IsDirectLaunchOn && IsRemoteServer && IsServerRunning ? Strings.Play_FikaStrayServer : null;

        if (!OtherServersOn || !IsDirectLaunchOn || IsRemoteServer || !_fikaFound.Server || Server?.ExePath is not { } exe)
        {
            FikaHostNote = null;
            FikaHostAddress = null;
            FikaHostWarning = null;
            return;
        }

        var root = ServerRootOf(installPath);
        if (_fikaHostRead is not { } read || read.Root != root || DateTime.UtcNow - read.At > TimeSpan.FromSeconds(15))
        {
            read = (root, DateTime.UtcNow, LocalNetworkAddresses.Find(), FikaServerConfig.Read(root));
            _fikaHostRead = read;
        }

        if (read.Config is not { } config)
        {
            FikaHostNote = null;
            FikaHostAddress = null;
            FikaHostWarning = FikaServerConfig.Find(root) is null ? Strings.Play_FikaHostNotWritten : null;
            return;
        }

        var port = config.Port?.ToString() ?? SptDirectLaunch.ServerAddress(exe).Split(':')[^1];
        var addresses = read.Addresses.Select(a => a + ":" + port).ToList();
        FikaHostAddress = addresses.FirstOrDefault();
        FikaHostNote = addresses.Count == 0
            ? Text(Strings.Play_FikaHostNoAddressFormat, port)
            : Text(Strings.Play_FikaHostFormat, TextLists.Join(addresses), port);
        FikaHostWarning = config.OnlyThisPc ? Text(Strings.Play_FikaHostOnlyThisPcFormat, config.Ip) : null;
    }

    [RelayCommand(CanExecute = nameof(CanCopyFikaAddress))]
    private void CopyFikaAddress()
    {
        if (FikaHostAddress is not { } address) return;

        try
        {
            Clipboard.SetText(address);
            PlayStatusIsError = false;
            PlayStatus = Text(Strings.Play_FikaHostCopiedFormat, address);
        }
        catch (COMException ex)
        {
            AppLog.Warn("Fika", $"couldn't copy the address: {ex.Message}");
        }
    }

    private bool CanCopyFikaAddress() => FikaHostAddress is not null;
}
