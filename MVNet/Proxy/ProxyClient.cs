using System.Net;
using System.Security;
using System.Net.Sockets;
using System.Web;

namespace MVNet
{
    /// <summary>
    /// Can produce proxied <see cref="TcpClient"/> instances.
    /// </summary>
    public abstract class ProxyClient
    {
        /// <summary>
        /// The proxy settings.
        /// </summary>
        public ProxySettings? Settings { get; }

        /// <summary>
        /// Instantiates a proxy client with the given <paramref name="settings"/>.
        /// </summary>
        public ProxyClient(ProxySettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// /// <summary>
        /// Create a proxied <see cref="TcpClient"/> to the destination host.
        /// </summary>
        /// <param name="destinationHost">The host you want to connect to</param>
        /// <param name="destinationPort">The port on which the host is listening</param>
        /// <param name="cancellationToken">A token to cancel the connection attempt</param>
        /// <param name="tcpClient">A <see cref="TcpClient"/> instance (if null, a new one will be created)</param>
        /// <exception cref="ArgumentException">Value of <paramref name="destinationHost"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Value of <paramref name="destinationPort"/> less than 1 or greater than 65535.</exception>
        /// <exception cref="ProxyException">Error while working with the proxy.</exception>
        public async Task<TcpClient> ConnectAsync(string destinationHost, int destinationPort, TimeSpan receiveTimeout, TimeSpan sendTimeout, TimeSpan connectTimeout, TcpClient tcpClient = null, CancellationToken cancellationToken = default)
        {
            var client = tcpClient ?? new TcpClient()
            {
                ReceiveTimeout = (int)receiveTimeout.TotalMilliseconds,
                SendTimeout = (int)sendTimeout.TotalMilliseconds
            };


            string host = string.Empty;
            int port = 0;

            // NoProxy case, connect directly to the server without proxy
            if (this is NoProxyClient)
            {
                host = destinationHost;
                port = destinationPort;
            }
            else
            {
                host = Settings.Host;
                port = Settings.Port;
            }
            

            // Try to connect to the proxy (or directly to the server in the NoProxy case)
            try
            {
                using var timeoutCts = new CancellationTokenSource(connectTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
                await client.ConnectAsync(host, port, linkedCts.Token).ConfigureAwait(false);

                await CreateConnectionAsync(client, destinationHost, destinationPort, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                client.Close();

                if (ex is SocketException or SecurityException)
                {
                    throw new ProxyException($"Failed to connect to {(this is NoProxyClient ? "server" : "proxy-server")}", ex);
                }

                throw;
            }

            return client;
        }

        /// <summary>
        /// Proxy protocol specific connection.
        /// </summary>
        /// <param name="tcpClient">The <see cref="TcpClient"/> that can be used to connect to the proxy over TCP</param>
        /// <param name="destinationHost">The target host that the proxy needs to connect to</param>
        /// <param name="destinationPort">The target port that the proxy needs to connect to</param>
        /// <param name="cancellationToken">A token to cancel operations</param>
        protected virtual Task CreateConnectionAsync(TcpClient tcpClient, string destinationHost, int destinationPort,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();


        public static ProxyClient Parse(Uri uri, NetworkCredential? credentials = default)
        {
            ProxySettings settings = new ProxySettings();
            settings.Host = uri.Host;
            settings.Port = uri.Port;
            if (credentials != null)
                settings.Credentials = credentials;

            switch (uri.Scheme)
            {
                case "http":
                    return new HttpProxyClient(settings);
                case "socks4":
                    return new Socks4ProxyClient(settings);
                case "socks4a":
                    return new Socks4aProxyClient(settings);
                case "socks5":
                    return new Socks5ProxyClient(settings);
                case "ap":
                    try
                    {
                        var data = Convert.FromHexString(uri.Host).ToStringArray();
                        if (data.Length == 3)
                        {
                            settings = new ProxySettings();
                            settings.Host = data[0];
                            settings.Port = Convert.ToInt32(data[1]);
                            if (credentials != null)
                                settings.Credentials = credentials;

                            return new AzadiProxyClient(data[2], settings);
                        }
                        else
                            throw new ProxyException("Azadi proxy parsing error");
                    }
                    catch (Exception ex)
                    {
                        throw new ProxyException("Azadi proxy parsing error", ex);
                    }
                default:
                    return new NoProxyClient();
            }
        }

        public static ProxyClient Parse(string uri, NetworkCredential? credentials = default)
        {
            Uri address = new UriBuilder(uri).Uri;

            ProxySettings settings = new ProxySettings();
            settings.Host = address.Host;
            settings.Port = address.Port;
            if (credentials != null)
                settings.Credentials = credentials;

            switch (address.Scheme)
            {
                case "http":
                    return new HttpProxyClient(settings);
                case "socks4":
                    return new Socks4ProxyClient(settings);
                case "socks4a":
                    return new Socks4aProxyClient(settings);
                case "socks5":
                    return new Socks5ProxyClient(settings);
                case "ap":
                    try
                    {
                        var data = Convert.FromHexString(address.Host).ToStringArray();
                        if (data.Length == 3)
                        {
                            settings = new ProxySettings();
                            settings.Host = data[0];
                            settings.Port = Convert.ToInt32(data[1]);
                            if (credentials != null)
                                settings.Credentials = credentials;

                            return new AzadiProxyClient(data[2], settings);
                        }
                        else
                            throw new ProxyException("Azadi proxy parsing error");
                    }
                    catch (Exception ex)
                    {
                        throw new ProxyException("Azadi proxy parsing error", ex);
                    }
                default:
                    return new NoProxyClient();

            }
        }
    }
}
