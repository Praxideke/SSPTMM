using TCFModManager.Core.Services;
using Xunit;

namespace TCFModManager.Core.Tests;

public class ForkFikaServerConfigTests
{
    private const string Sample = """
        {
            "client": { "useBtr": true, },
            "server": {
                "SPT": {
                    "http": {
                        "ip": "0.0.0.0", // the interface to listen on
                        "port": 7000, // the port to host on
                        "backendIp": "84.20.1.9",
                        "backendPort": 7000
                    },
                    "disableSPTChatBots": false
                }
            }
        }
        """;

    [Fact]
    public void Reads_the_address_with_comments_and_trailing_commas()
    {
        var config = FikaServerConfig.Parse(Sample)!;

        Assert.Equal("0.0.0.0", config.Ip);
        Assert.Equal(7000, config.Port);
        Assert.Equal("84.20.1.9", config.BackendIp);
        Assert.Equal(7000, config.BackendPort);
        Assert.False(config.OnlyThisPc);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("localhost", true)]
    [InlineData("192.168.1.20", false)]
    public void Knows_an_address_only_this_pc_can_reach(string ip, bool only) =>
        Assert.Equal(only, FikaServerConfig.Parse(Sample.Replace("\"0.0.0.0\"", $"\"{ip}\""))!.OnlyThisPc);

    [Fact]
    public void A_file_without_the_section_has_nothing_in_it() =>
        Assert.Equal(new FikaServerConfig(null, null, null, null), FikaServerConfig.Parse("""{ "client": {} }"""));

    [Fact]
    public void Text_that_is_not_json_is_unreadable() =>
        Assert.Null(FikaServerConfig.Parse("not json {"));

    [Fact]
    public void Finds_the_file_in_fikas_server_mod_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "fika-config-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Null(FikaServerConfig.Read(root));

            var configs = Path.Combine(root, "user", "mods", "Fika.Server", "assets", "configs");
            Directory.CreateDirectory(configs);
            File.WriteAllText(Path.Combine(configs, "fika.jsonc"), Sample);

            Assert.Equal(7000, FikaServerConfig.Read(root)!.Port);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
