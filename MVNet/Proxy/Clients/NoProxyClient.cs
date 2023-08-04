using System.Net.Sockets;

namespace MVNet
{
    /// <summary>
    /// A dummy client that does not proxy the connection.
    /// </summary>
    public class NoProxyClient : ProxyClient
    {
        /// <summary>
        /// Provides unproxied connections.
        /// </summary>
        public NoProxyClient() : base(new ProxySettings())
        {

        }

        /// <inheritdoc/>
        protected override Task CreateConnectionAsync(TcpClient client, string destinationHost, int destinationPort,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(destinationHost))
            {
                throw new ArgumentException(null, nameof(destinationHost));
            }

            if (!PortHelper.ValidateTcpPort(destinationPort))
            {
                throw new ArgumentOutOfRangeException(nameof(destinationPort));
            }

            if (client == null || !client.Connected)
            {
                throw new SocketException();
            }

            return Task.CompletedTask;
        }
    }
}
