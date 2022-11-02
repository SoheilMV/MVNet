namespace MVNet
{
    internal static class ProxyHelper
    {
        public static ProxyClient CreateProxyClient(ProxyType proxyType, string host = null, int port = 0, string username = null, string password = null, string secret = null)
        {
            switch (proxyType)
            {
                case ProxyType.HTTP:
                    return port == 0 ?
                        new HttpProxyClient(host) : new HttpProxyClient(host, port, username, password);

                case ProxyType.Socks4:
                    return port == 0 ?
                        new Socks4ProxyClient(host) : new Socks4ProxyClient(host, port, username);

                case ProxyType.Socks4A:
                    return port == 0 ?
                        new Socks4AProxyClient(host) : new Socks4AProxyClient(host, port, username);

                case ProxyType.Socks5:
                    return port == 0 ?
                        new Socks5ProxyClient(host) : new Socks5ProxyClient(host, port, username, password);

                case ProxyType.Azadi:
                    return (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)) ? new AzadiProxyClient(secret, host, port, username, password) : new AzadiProxyClient(secret, host, port);

                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
