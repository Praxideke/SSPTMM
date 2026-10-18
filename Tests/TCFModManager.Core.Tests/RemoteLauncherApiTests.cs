using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TCFModManager.Core.ServerMap;
using TCFModManager.Core.Services;
using Xunit;

namespace TCFModManager.Core.Tests;

//
// Fork (1.4.3): Play against another PC's SPT server (a Fika host). Its certificate is self-signed,
// so it is trusted on first use and pinned; a different one after that is refused. Run against a
// TLS server on loopback that answers the launcher's ping as SPT does.
//
public sealed class RemoteLauncherApiTests
{
    [Fact]
    public async Task The_first_certificate_is_taken_the_same_one_after_and_another_refused()
    {
        using var first = SelfSigned();
        using var other = SelfSigned();
        var thumbprint = ServerCertificatePin.Thumbprint(first);

        await using (var server = PongServer.Start(first))
        {
            using var api = SptLauncherApi.ForRemote(server.Address, pinnedThumbprint: null);
            Assert.True(await api.PingAsync());
            Assert.Equal(thumbprint, api.PresentedThumbprint);
            Assert.False(api.CertificateChanged);
        }

        await using (var server = PongServer.Start(first))
        {
            using var api = SptLauncherApi.ForRemote(server.Address, thumbprint);
            Assert.True(await api.PingAsync());
        }

        await using (var server = PongServer.Start(other))
        {
            using var api = SptLauncherApi.ForRemote(server.Address, thumbprint);
            await Assert.ThrowsAsync<HttpRequestException>(() => api.PingAsync());
            Assert.True(api.CertificateChanged);
            Assert.Equal(ServerCertificatePin.Thumbprint(other), api.PresentedThumbprint);
        }
    }

    private static X509Certificate2 SelfSigned()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var made = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        // Windows' TLS wants a key it can keep: through a PFX and back.
        return X509CertificateLoader.LoadPkcs12(made.Export(X509ContentType.Pfx), null);
    }

    // Answers every request with SPT's zlib-wrapped { "Response": "Pong!" }.
    private sealed class PongServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;

        private PongServer(X509Certificate2 certificate)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _loop = Task.Run(() => ServeAsync(certificate));
        }

        public string Address => $"127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

        public static PongServer Start(X509Certificate2 certificate) => new(certificate);

        private async Task ServeAsync(X509Certificate2 certificate)
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stop.Token);
                }
                catch (Exception) when (_stop.IsCancellationRequested)
                {
                    return;
                }

                _ = Task.Run(async () =>
                {
                    using (client)
                    {
                        try
                        {
                            await using var tls = new SslStream(client.GetStream());
                            await tls.AuthenticateAsServerAsync(certificate);
                            var buffer = new byte[4096];
                            var request = new StringBuilder();
                            while (!request.ToString().Contains("\r\n\r\n"))
                            {
                                var read = await tls.ReadAsync(buffer);
                                if (read == 0) return;
                                request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                            }

                            var body = SptLauncherApi.Compress("{\"Response\":\"Pong!\"}");
                            var head = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                            await tls.WriteAsync(head);
                            await tls.WriteAsync(body);
                        }
                        catch (Exception ex) when (ex is IOException or System.Security.Authentication.AuthenticationException)
                        {
                            // The client refused the certificate.
                        }
                    }
                });
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _listener.Stop();
            await _loop;
            _stop.Dispose();
        }
    }
}
