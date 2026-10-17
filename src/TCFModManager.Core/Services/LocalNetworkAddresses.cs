using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TCFModManager.Core.Services;

//
// Fork (1.4.2): this PC's addresses that another PC could reach it at - for the Play card's note to
// a Fika host about what to give friends. IPv4 only, from network adapters that are up: home
// networks first, then Tailscale's, then any other; never loopback or link-local.
//
public static class LocalNetworkAddresses
{
    public static IReadOnlyList<string> Find()
    {
        var found = new List<IPAddress>();
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (Rank(unicast.Address) is not null) found.Add(unicast.Address);
                }
            }
        }
        catch (NetworkInformationException ex)
        {
            AppLog.Debug("Network", $"couldn't list this PC's addresses: {ex.Message}");
        }

        return Order(found);
    }

    public static IReadOnlyList<string> Order(IEnumerable<IPAddress> addresses) =>
        addresses
            .Where(a => Rank(a) is not null)
            .Distinct()
            .OrderBy(a => Rank(a))
            .Select(a => a.ToString())
            .ToList();

    // 0 a home network's, 1 Tailscale's (100.64.0.0/10), 2 anything else; null for none to give out.
    public static int? Rank(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address)) return null;

        var b = address.GetAddressBytes();
        if (b[0] == 169 && b[1] == 254) return null;
        if (b[0] == 0 || b[0] >= 224) return null;

        if (b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168)) return 0;
        if (b[0] == 100 && b[1] is >= 64 and <= 127) return 1;
        return 2;
    }
}
