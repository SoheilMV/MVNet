using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents a client for a Socks5 proxy server.
    /// </summary>
    public sealed class Socks5ProxyClient : ProxyClient
    {
        #region Constants (private)

        private const int DefaultPort = 1080;

        private const byte VersionNumber = 5;
        private const byte Reserved = 0x00;
        private const byte AuthMethodNoAuthenticationRequired = 0x00;
        //private const byte AuthMethodGssapi = 0x01;
        private const byte AuthMethodUsernamePassword = 0x02;
        //private const byte AuthMethodIanaAssignedRangeBegin = 0x03;
        //private const byte AuthMethodIanaAssignedRangeEnd = 0x7f;
        //private const byte AuthMethodReservedRangeBegin = 0x80;
        //private const byte AuthMethodReservedRangeEnd = 0xfe;
        private const byte AuthMethodReplyNoAcceptableMethods = 0xff;
        private const byte CommandConnect = 0x01;
        //private const byte CommandBind = 0x02;
        //private const byte CommandUdpAssociate = 0x03;
        private const byte CommandReplySucceeded = 0x00;
        private const byte CommandReplyGeneralSocksServerFailure = 0x01;
        private const byte CommandReplyConnectionNotAllowedByRuleset = 0x02;
        private const byte CommandReplyNetworkUnreachable = 0x03;
        private const byte CommandReplyHostUnreachable = 0x04;
        private const byte CommandReplyConnectionRefused = 0x05;
        // ReSharper disable once InconsistentNaming
        private const byte CommandReplyTTLExpired = 0x06;
        private const byte CommandReplyCommandNotSupported = 0x07;
        private const byte CommandReplyAddressTypeNotSupported = 0x08;
        private const byte AddressTypeIPv4 = 0x01;
        private const byte AddressTypeDomainName = 0x03;
        private const byte AddressTypeIPv6 = 0x04;

        #endregion


        #region Constructors (public)

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.Socks5ProxyClient" />.
        /// </summary>
        public Socks5ProxyClient() : this(null)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.Socks5ProxyClient" /> the specified data about the proxy server.
        /// </summary>
        /// <param name="host">Proxy host.</param>
        /// <param name="port">Proxy port.</param>
        public Socks5ProxyClient(string host, int port = DefaultPort) : this(host, port, string.Empty, string.Empty)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.Socks5ProxyClient" /> the specified data about the proxy server.
        /// </summary>
        /// <param name="host">Proxy host.</param>
        /// <param name="port">Proxy port.</param>
        /// <param name="username">Username for authorization on the proxy server.</param>
        /// <param name="password">Password for authorization on the proxy server.</param>
        public Socks5ProxyClient(string host, int port, string username, string password) : base(ProxyType.Socks5, host, port, username, password)
        {
        }

        #endregion


        #region Static methods (public)

        /// <summary>
        /// Converts a string to an instance of a class <see cref="Socks5ProxyClient"/>.
        /// </summary>
        /// <param name="proxyAddress">A string of the form - host:port:username:password. The last three parameters are optional.</param>
        /// <returns>Class instance <see cref="Socks5ProxyClient"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="proxyAddress"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="proxyAddress"/> is an empty string.</exception>
        /// <exception cref="System.FormatException">The port format is incorrect.</exception>
        public new static Socks5ProxyClient Parse(string proxyAddress)
        {
            return Parse(ProxyType.Socks5, proxyAddress) as Socks5ProxyClient;
        }

        /// <summary>
        /// Converts a string to an instance of a class <see cref="Socks5ProxyClient"/>. Gets a value indicating whether the conversion was successful.
        /// </summary>
        /// <param name="proxyAddress">A string of the form - host:port:user_name:password. The last three parameters are optional.</param>
        /// <param name="result">If the conversion is successful, then contains an instance of the class <see cref="Socks5ProxyClient"/>, otherwise <see langword="null"/>.</param>
        /// <returns>Value <see langword="true"/>, if parameter <paramref name="proxyAddress"/> converted successfully, otherwise <see langword="false"/>.</returns>
        public static bool TryParse(string proxyAddress, out Socks5ProxyClient result)
        {
            if (!ProxyClient.TryParse(ProxyType.Socks5, proxyAddress, out var proxy))
            {
                result = null;
                return false;
                
            }

            result = proxy as Socks5ProxyClient;
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
        /// Property value <see cref="!:Port" /> less than 1 or greater than 65535.
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
                var nStream = curTcpClient.GetStream();

                InitialNegotiation(nStream);
                SendCommand(nStream, CommandConnect, destinationHost, destinationPort);
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

        #region Methods (private)

        private void InitialNegotiation(Stream nStream)
        {
            byte authMethod = !string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password)
                ? AuthMethodUsernamePassword
                : AuthMethodNoAuthenticationRequired;

            // +----+----------+----------+
            // |VER | NMETHODS | METHODS  |
            // +----+----------+----------+
            // | 1  |    1     | 1 to 255 |
            // +----+----------+----------+
            var request = new byte[3];

            request[0] = VersionNumber;
            request[1] = 1;
            request[2] = authMethod;

            nStream.Write(request, 0, request.Length);

            // +----+--------+
            // |VER | METHOD |
            // +----+--------+
            // | 1  |   1    |
            // +----+--------+
            var response = new byte[2];

            nStream.Read(response, 0, response.Length);

            byte reply = response[1];

            if (authMethod == AuthMethodUsernamePassword && reply == AuthMethodUsernamePassword)
                SendUsernameAndPassword(nStream);
            else if (reply != CommandReplySucceeded)
                HandleCommandError(reply);
        }

        private void SendUsernameAndPassword(Stream nStream)
        {
            var username = string.IsNullOrEmpty(_username) ?
                new byte[0] : Encoding.ASCII.GetBytes(_username);

            var password = string.IsNullOrEmpty(_password) ?
                new byte[0] : Encoding.ASCII.GetBytes(_password);

            // +----+------+----------+------+----------+
            // |VER | ULEN |  UNAME   | PLEN |  PASSWD  |
            // +----+------+----------+------+----------+
            // | 1  |  1   | 1 to 255 |  1   | 1 to 255 |
            // +----+------+----------+------+----------+
            var request = new byte[username.Length + password.Length + 3];

            request[0] = 1;
            request[1] = (byte)username.Length;
            username.CopyTo(request, 2);
            request[2 + username.Length] = (byte)password.Length;
            password.CopyTo(request, 3 + username.Length);

            nStream.Write(request, 0, request.Length);

            // +----+--------+
            // |VER | STATUS |
            // +----+--------+
            // | 1  |   1    |
            // +----+--------+
            var response = new byte[2];

            nStream.Read(response, 0, response.Length);

            byte reply = response[1];
            if (reply != CommandReplySucceeded)
                throw NewProxyException(Constants.ProxyException_Socks5_FailedAuthOn);
        }

        private void SendCommand(Stream nStream, byte command, string destinationHost, int destinationPort)
        {
            byte aTyp = GetAddressType(destinationHost);
            var dstAddress = GetAddressBytes(aTyp, destinationHost);
            var dstPort = GetPortBytes(destinationPort);

            // +----+-----+-------+------+----------+----------+
            // |VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
            // +----+-----+-------+------+----------+----------+
            // | 1  |  1  | X'00' |  1   | Variable |    2     |
            // +----+-----+-------+------+----------+----------+
            var request = new byte[4 + dstAddress.Length + 2];

            request[0] = VersionNumber;
            request[1] = command;
            request[2] = Reserved;
            request[3] = aTyp;
            dstAddress.CopyTo(request, 4);
            dstPort.CopyTo(request, 4 + dstAddress.Length);

            nStream.Write(request, 0, request.Length);

            // +----+-----+-------+------+----------+----------+
            // |VER | REP |  RSV  | ATYP | BND.ADDR | BND.PORT |
            // +----+-----+-------+------+----------+----------+
            // | 1  |  1  | X'00' |  1   | Variable |    2     |
            // +----+-----+-------+------+----------+----------+
            var response = new byte[255];

            nStream.Read(response, 0, response.Length);

            byte reply = response[1];

            // If the request is not completed.
            if (reply != CommandReplySucceeded)
                HandleCommandError(reply);
        }

        private byte GetAddressType(string host)
        {
            if (!IPAddress.TryParse(host, out var ipAddress))
                return AddressTypeDomainName;

            switch (ipAddress.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    return AddressTypeIPv4;

                case AddressFamily.InterNetworkV6:
                    return AddressTypeIPv6;

                default:
                    throw new ProxyException(string.Format(Constants.ProxyException_NotSupportedAddressType,
                        host, Enum.GetName(typeof(AddressFamily), ipAddress.AddressFamily), ToString()), this);
            }
        }

        private static byte[] GetAddressBytes(byte addressType, string host)
        {
            switch (addressType)
            {
                case AddressTypeIPv4:
                case AddressTypeIPv6:
                    return IPAddress.Parse(host).GetAddressBytes();

                case AddressTypeDomainName:
                    var bytes = new byte[host.Length + 1];

                    bytes[0] = (byte)host.Length;
                    Encoding.ASCII.GetBytes(host).CopyTo(bytes, 1);

                    return bytes;

                default:
                    return null;
            }
        }

        private static byte[] GetPortBytes(int port)
        {
            var array = new byte[2];

            array[0] = (byte)(port / 256);
            array[1] = (byte)(port % 256);

            return array;
        }

        private void HandleCommandError(byte command)
        {
            string errorMessage;

            switch (command)
            {
                case AuthMethodReplyNoAcceptableMethods:
                    errorMessage = Constants.Socks5_AuthMethodReplyNoAcceptableMethods;
                    break;

                case CommandReplyGeneralSocksServerFailure:
                    errorMessage = Constants.Socks5_CommandReplyGeneralSocksServerFailure;
                    break;

                case CommandReplyConnectionNotAllowedByRuleset:
                    errorMessage = Constants.Socks5_CommandReplyConnectionNotAllowedByRuleset;
                    break;

                case CommandReplyNetworkUnreachable:
                    errorMessage = Constants.Socks5_CommandReplyNetworkUnreachable;
                    break;

                case CommandReplyHostUnreachable:
                    errorMessage = Constants.Socks5_CommandReplyHostUnreachable;
                    break;

                case CommandReplyConnectionRefused:
                    errorMessage = Constants.Socks5_CommandReplyConnectionRefused;
                    break;

                case CommandReplyTTLExpired:
                    errorMessage = Constants.Socks5_CommandReplyTTLExpired;
                    break;

                case CommandReplyCommandNotSupported:
                    errorMessage = Constants.Socks5_CommandReplyCommandNotSupported;
                    break;

                case CommandReplyAddressTypeNotSupported:
                    errorMessage = Constants.Socks5_CommandReplyAddressTypeNotSupported;
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
