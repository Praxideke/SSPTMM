using System.Net;
using TCFModManager.Core.Services;
using Xunit;

namespace TCFModManager.Core.Tests;

public class ForkLocalNetworkAddressesTests
{
    [Theory]
    [InlineData("192.168.1.20", 0)]
    [InlineData("10.0.0.5", 0)]
    [InlineData("172.16.0.1", 0)]
    [InlineData("172.31.255.1", 0)]
    [InlineData("100.64.0.1", 1)]
    [InlineData("100.127.1.1", 1)]
    [InlineData("172.32.0.1", 2)]
    [InlineData("100.128.0.1", 2)]
    [InlineData("84.20.1.9", 2)]
    public void Ranks_addresses_to_give_out(string text, int rank) =>
        Assert.Equal(rank, LocalNetworkAddresses.Rank(IPAddress.Parse(text)));

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.10.2")]
    [InlineData("0.0.0.0")]
    [InlineData("239.1.1.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    public void Leaves_out_addresses_nobody_else_can_reach(string text) =>
        Assert.Null(LocalNetworkAddresses.Rank(IPAddress.Parse(text)));

    [Fact]
    public void Home_network_first_then_tailscale_then_the_rest_once_each()
    {
        var ordered = LocalNetworkAddresses.Order(
            new[] { "84.20.1.9", "100.70.1.2", "127.0.0.1", "192.168.1.20", "192.168.1.20" }.Select(IPAddress.Parse));

        Assert.Equal(["192.168.1.20", "100.70.1.2", "84.20.1.9"], ordered);
    }
}
