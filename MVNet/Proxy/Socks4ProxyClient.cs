using System.Net;
using System.Text;
using System.Net.Sockets;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents a client for a Socks4 proxy server.
    /// </summary>
    public class Socks4ProxyClient : ProxyClient
    {
        #region Constants (protected)

        protected const int DefaultPort = 1080;

        protected const byte VersionNumber = 4;

        private const byte CommandConnect = 0x01;
        // protected const byte CommandBind = 0x02;
        protected const byte CommandReplyRequestGranted = 0x5a;
        private const byte CommandReplyRequestRejectedOrFailed = 0x5b;
        private const byte CommandReplyRequestRejectedCannotConnectToIdentd = 0x5c;
        private const byte CommandReplyRequestRejectedDifferentIdentd = 0x5d;

        #endregion


        #region Constructors (public)

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.Socks4ProxyClient" />.
        /// </summary>
        public Socks4ProxyClient() : this(null)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.Socks4ProxyClient" /> the specified data about the proxy server.
        /// </summary>
        /// <param name="host">Proxy host.</param>
        /// <param name="port">Proxy port.</param>
        public Socks4ProxyClient(string host, int port = DefaultPort) : this(host, port, string.Empty)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.Socks4ProxyClient" /> the specified data about the proxy server.
        /// </summary>
        /// <param name="host">Proxy host.</param>
        /// <param name="port">Proxy port.</param>
        /// <param name="username">Username for authorization on the proxy server.</param>
        public Socks4ProxyClient(string host, int port, string username) : base(ProxyType.Socks4, host, port, username, null)
        {
        }

        #endregion


        #region Static methods (Public)

        /// <summary>
        /// Converts a string to an instance of a class <see cref="Socks4ProxyClient"/>.
        /// </summary>
        /// <param name="proxyAddress">A string of the form - host:port:username:password. The last three parameters are optional.</param>
        /// <returns>Class instance <see cref="Socks4ProxyClient"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="proxyAddress"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="proxyAddress"/> is an empty string.</exception>
        /// <exception cref="System.FormatException">The port format is incorrect.</exception>
        public new static Socks4ProxyClient Parse(string proxyAddress)
        {
            return Parse(ProxyType.Socks4, proxyAddress) as Socks4ProxyClient;
        }

        /// <summary>
        /// Converts a string to an instance of a class <see cref="Socks4ProxyClient"/>. Gets a value indicating whether the conversion was successful.
        /// </summary>
        /// <param name="proxyAddress">A string of the form - host:port:username:password. The last three parameters are optional.</param>
        /// <param name="result">If the conversion is successful, then contains an instance of the class <see cref="Socks4ProxyClient"/>, otherwise <see langword="null"/>.</param>
        /// <returns>Value <see langword="true"/>, if parameter <paramref name="proxyAddress"/> converted successfully, otherwise <see langword="false"/>.</returns>
        public static bool TryParse(string proxyAddress, out Socks4ProxyClient result)
        {
            if (!TryParse(ProxyType.Socks4, proxyAddress, out var proxy))
            {
                result = null;
                return false;    
            }

            result = proxy as Socks4ProxyClient;
            return true;
        }

        #endregion


        /// <inheritdoc />
        /// <summary>
        /// Creates a connection to the server through a proxy server.
        /// </summary>
        /// <param name="destinationHost">The host of the server to contact through the proxy.</param>
        /// <param name="destinationPort">The port of the server to contact through the proxy server.</param>
        /// <param name="tcpClient">Connection through which to work, or value <see langword="null" />.</param>
        /// <returns>Connecting to the server through a proxy server.</returns>
        /// <exception cref="T:System.InvalidOperationException">
        /// Property value <see cref="!:Host" /> equals <see langword="null" /> or has zero length.
        /// -or-
        /// Property value <see cref="!:Port" />less than 1 or greater than 65535.
        /// -or-
        /// Property value <see cref="!:Username" /> is longer than 255 characters.
        /// -or-
        /// Property value <see cref="!:Password" /> is longer than 255 characters.
        /// </exception>
        /// <exception cref="T:System.ArgumentNullException">Parameter value <paramref name="destinationHost" /> equals <see langword="null" />.</exception>
        /// <exception cref="T:System.ArgumentException">Parameter value <paramref name="destinationHost" /> is an empty string.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">Parameter value <paramref name="destinationPort" /> less than 1 or greater than 65535.</exception>
        /// <exception cref="!:MVNet.ProxyException">Error while working with proxy server.</exception>
        public override TcpClient CreateConnection(string destinationHost, int destinationPort, TcpClient tcpClient = null)
        {
            CheckState();

            #region Parameter Check

            if (destinationHost == null)
                throw new ArgumentNullException(nameof(destinationHost));

            if (destinationHost.Length == 0)
                throw ExceptionHelper.EmptyString(nameof(destinationHost));

            if (!ExceptionHelper.ValidateTcpPort(destinationPort))
                throw ExceptionHelper.WrongTcpPort(nameof(destinationHost));

            #endregion

            var curTcpClient = tcpClient ?? CreateConnectionToProxy();

            try
            {
                SendCommand(curTcpClient.GetStream(), CommandConnect, destinationHost, destinationPort);
            }
            catch (Exception ex)
            {
                curTcpClient.Close();

                if (ex is IOException || ex is SocketException)
                    throw NewProxyException(Constants.ProxyException_Error, ex);

                throw;
            }

            return curTcpClient;
        }

        #region Methods (internal protected)

        private void SendCommand(NetworkStream nStream, byte command, string destinationHost, int destinationPort)
        {
            var dstPort = GetIpAddressBytes(destinationHost);
            var dstIp = GetPortBytes(destinationPort);

            var userId = string.IsNullOrEmpty(_username) ?
                new byte[0] : Encoding.ASCII.GetBytes(_username);

            // +----+----+----+----+----+----+----+----+----+----+....+----+
            // | VN | CD | DSTPORT |      DSTIP        | USERID       |NULL|
            // +----+----+----+----+----+----+----+----+----+----+....+----+
            //    1    1      2              4           variable       1
            var request = new byte[9 + userId.Length];

            request[0] = VersionNumber;
            request[1] = command;
            dstIp.CopyTo(request, 2);
            dstPort.CopyTo(request, 4);
            userId.CopyTo(request, 8);
            request[8 + userId.Length] = 0x00;

            nStream.Write(request, 0, request.Length);

            // +----+----+----+----+----+----+----+----+
            // | VN | CD | DSTPORT |      DSTIP        |
            // +----+----+----+----+----+----+----+----+
            //   1    1       2              4
            var response = new byte[8];

            nStream.Read(response, 0, response.Length);

            byte reply = response[1];

            // If the request is not completed.
            if (reply != CommandReplyRequestGranted)
                HandleCommandError(reply);
        }

        private byte[] GetIpAddressBytes(string destinationHost)
        {
            if (IPAddress.TryParse(destinationHost, out var ipAddress))
                return ipAddress.GetAddressBytes();

            try
            {
                var ips = Dns.GetHostAddresses(destinationHost);

                if (ips.Length > 0)
                    ipAddress = ips[0];
            }
            catch (Exception ex)
            {
                if (ex is SocketException || ex is ArgumentException)
                {
                    throw new ProxyException(string.Format(Constants.ProxyException_FailedGetHostAddresses, destinationHost), this, ex);
                }

                throw;
            }

            return ipAddress.GetAddressBytes();
        }

        protected static byte[] GetPortBytes(int port)
        {
            var array = new byte[2];

            array[0] = (byte)(port / 256);
            array[1] = (byte)(port % 256);

            return array;
        }

        protected void HandleCommandError(byte command)
        {
            string errorMessage;

            switch (command)
            {
                case CommandReplyRequestRejectedOrFailed:
                    errorMessage = Constants.Socks4_CommandReplyRequestRejectedOrFailed;
                    break;

                case CommandReplyRequestRejectedCannotConnectToIdentd:
                    errorMessage = Constants.Socks4_CommandReplyRequestRejectedCannotConnectToIdentd;
                    break;

                case CommandReplyRequestRejectedDifferentIdentd:
                    errorMessage = Constants.Socks4_CommandReplyRequestRejectedDifferentIdentd;
                    break;

                default:
                    errorMessage = Constants.UnknownError;
                    break;
            }

            string exceptionMsg = string.Format(Constants.ProxyException_CommandError, errorMessage, ToString());

            throw new ProxyException(exceptionMsg, this);
        }

        #endregion
    }
}
