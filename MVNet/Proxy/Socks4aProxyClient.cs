using System.Net.Sockets;
using System.Text;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents a client for a Socks4a proxy.
    /// </summary>
    public sealed class Socks4AProxyClient : Socks4ProxyClient
    {
        #region Constructors (public)

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.Socks4AProxyClient" />.
        /// </summary>
        public Socks4AProxyClient() : this(null)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.Socks4AProxyClient" /> the specified data about the proxy server.
        /// </summary>
        /// <param name="host">Proxy host.</param>
        /// <param name="port">Proxy port.</param>
        public Socks4AProxyClient(string host, int port = DefaultPort) : this(host, port, string.Empty)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.Socks4AProxyClient" /> the specified data about the proxy server.
        /// </summary>
        /// <param name="host">Proxy host.</param>
        /// <param name="port">Proxy port.</param>
        /// <param name="username">Username for authorization on the proxy server.</param>
        public Socks4AProxyClient(string host, int port, string username) : base(host, port, username)
        {
            _type = ProxyType.Socks4A;
        }

        #endregion


        #region Methods (public)

        /// <summary>
        /// Converts a string to an instance of a class <see cref="Socks4AProxyClient"/>.
        /// </summary>
        /// <param name="proxyAddress">A string of the form - host:port:username:password. The last three parameters are optional.</param>
        /// <returns>class instance <see cref="Socks4AProxyClient"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="proxyAddress"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="proxyAddress"/> is an empty string.</exception>
        /// <exception cref="System.FormatException">The port format is incorrect.</exception>
        public new static Socks4AProxyClient Parse(string proxyAddress)
        {
            return Parse(ProxyType.Socks4A, proxyAddress) as Socks4AProxyClient;
        }

        /// <summary>
        /// Converts a string to an instance of a class <see cref="Socks4AProxyClient"/>. Gets a value indicating whether the conversion was successful.
        /// </summary>
        /// <param name="proxyAddress">A string of the form - host:port:username:password. The last three parameters are optional.</param>
        /// <param name="result">If the conversion is successful, then contains an instance of the class <see cref="Socks4AProxyClient"/>, otherwise <see langword="null"/>.</param>
        /// <returns>Value <see langword="true"/>, if parameter <paramref name="proxyAddress"/> converted successfully, otherwise <see langword="false"/>.</returns>
        public static bool TryParse(string proxyAddress, out Socks4AProxyClient result)
        {
            if (!TryParse(ProxyType.Socks4A, proxyAddress, out var proxy))
            {
                result = null;
                return false;
            }

            result = proxy as Socks4AProxyClient;
            return true;
        }

        #endregion


        internal void SendCommand(NetworkStream nStream, byte command, string destinationHost, int destinationPort)
        {
            var dstPort = GetPortBytes(destinationPort);
            byte[] dstIp = { 0, 0, 0, 1 };

            var userId = string.IsNullOrEmpty(_username) ?
                new byte[0] : Encoding.ASCII.GetBytes(_username);

            var dstAddress = Encoding.ASCII.GetBytes(destinationHost);

            // +----+----+----+----+----+----+----+----+----+----+....+----+----+----+....+----+
            // | VN | CD | DSTPORT |      DSTIP        | USERID       |NULL| DSTADDR      |NULL|
            // +----+----+----+----+----+----+----+----+----+----+....+----+----+----+....+----+
            //    1    1      2              4           variable       1    variable        1 
            var request = new byte[10 + userId.Length + dstAddress.Length];

            request[0] = VersionNumber;
            request[1] = command;
            dstPort.CopyTo(request, 2);
            dstIp.CopyTo(request, 4);
            userId.CopyTo(request, 8);
            request[8 + userId.Length] = 0x00;
            dstAddress.CopyTo(request, 9 + userId.Length);
            request[9 + userId.Length + dstAddress.Length] = 0x00;

            nStream.Write(request, 0, request.Length);

            // +----+----+----+----+----+----+----+----+
            // | VN | CD | DSTPORT |      DSTIP        |
            // +----+----+----+----+----+----+----+----+
            //    1    1      2              4
            var response = new byte[8];

            nStream.Read(response, 0, 8);

            byte reply = response[1];

            // If the request is not completed.
            if (reply != CommandReplyRequestGranted)
                HandleCommandError(reply);
        }
    }
}
