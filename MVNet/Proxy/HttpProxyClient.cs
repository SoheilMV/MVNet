using System.Text;
using System.Net;
using System.Net.Sockets;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents a client for an HTTP proxy.
    /// </summary>
    public sealed class HttpProxyClient : ProxyClient
    {
        #region Constants (private)

        private const int BufferSize = 50;
        private const int DefaultPort = 8080;

        #endregion

        // TODO: hide constructors and make ProxyClient Factory: ProxyClient.ParseHttp / ProxyClient.ParseSocks4 / ProxyClient.ParseSocks5

        #region Constructors (public)

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.HttpProxyClient" />.
        /// </summary>
        public HttpProxyClient() : this(null)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.HttpProxyClient" /> the specified data about the proxy server.
        /// </summary>
        /// <param name="host">Proxy host.</param>
        /// <param name="port">Proxy port.</param>
        public HttpProxyClient(string host, int port = DefaultPort) : this(host, port, string.Empty, string.Empty)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.HttpProxyClient" /> the specified data about the proxy server.
        /// </summary>
        /// <param name="host">Proxy host.</param>
        /// <param name="port">Proxy port.</param>
        /// <param name="username">Username for authorization on the proxy server.</param>
        /// <param name="password">Password for authorization on the proxy server.</param>
        public HttpProxyClient(string host, int port, string username, string password) : base(ProxyType.HTTP, host, port, username, password)
        {
        }

        #endregion


        #region Static properties (public)
        /// <summary>
        /// The version of the protocol to be used. HTTP 2.0 is not currently supported.
        /// </summary>
        public static string ProtocolVersion { get; set; } = "1.1";

        #endregion


        #region Static methods (public)

        /// <summary>
        /// Converts a string to an instance of a class <see cref="HttpProxyClient"/>.
        /// </summary>
        /// <param name="proxyAddress">A string of the form - host:port:username:password. The last three parameters are optional.</param>
        /// <returns>Class instance <see cref="HttpProxyClient"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="proxyAddress"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="proxyAddress"/> is an empty string.</exception>
        /// <exception cref="System.FormatException">The port format is incorrect.</exception>
        public new static HttpProxyClient Parse(string proxyAddress)
        {
            return Parse(ProxyType.HTTP, proxyAddress) as HttpProxyClient;
        }

        /// <summary>
        /// Converts a string to an instance of a class <see cref="HttpProxyClient"/>. Gets a value indicating whether the conversion was successful.
        /// </summary>
        /// <param name="proxyAddress">A string of the form - host:port:username:password. The last three parameters are optional.</param>
        /// <param name="result">If the conversion is successful, then contains an instance of the class <see cref="HttpProxyClient"/>, otherwise <see langword="null"/>.</param>
        /// <returns>Value <see langword="true"/>, if parameter <paramref name="proxyAddress"/> converted successfully, otherwise <see langword="false"/>.</returns>
        public static bool TryParse(string proxyAddress, out HttpProxyClient result)
        {
            if (!TryParse(ProxyType.HTTP, proxyAddress, out var proxy))
            {
                result = null;
                return false;
            }

            result = proxy as HttpProxyClient;
            return true;
        }

        #endregion

        #region Methods (public)

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
        /// <remarks>If the server port is not 80, then the 'CONNECT' method is used to connect.</remarks>
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

            if (destinationPort == 80)
                return curTcpClient;

            HttpStatusCode statusCode;

            try
            {
                var nStream = curTcpClient.GetStream();

                SendConnectionCommand(nStream, destinationHost, destinationPort);
                statusCode = ReceiveResponse(nStream);
            }
            catch (Exception ex)
            {
                curTcpClient.Close();

                if (ex is IOException || ex is SocketException)
                    throw NewProxyException(Constants.ProxyException_Error, ex);

                throw;
            }

            if (statusCode == HttpStatusCode.OK)
                return curTcpClient;

            curTcpClient.Close();

            throw new ProxyException(string.Format(Constants.ProxyException_ReceivedWrongStatusCode, statusCode, ToString()), this);
        }

        #endregion


        #region Methods (private)

        private string GenerateAuthorizationHeader()
        {
            if (string.IsNullOrEmpty(_username) && string.IsNullOrEmpty(_password))
                return string.Empty;

            string data = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{_username}:{_password}"));

            return $"Proxy-Authorization: Basic {data}\r\n";
        }

        private void SendConnectionCommand(Stream nStream, string destinationHost, int destinationPort)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("CONNECT {0}:{1} HTTP/{2}\r\n", destinationHost, destinationPort, ProtocolVersion);
            sb.AppendFormat(GenerateAuthorizationHeader());
            sb.Append("Host: "); sb.AppendLine(destinationHost);
            sb.AppendLine("Proxy-Connection: Keep-Alive");
            sb.AppendLine();

            var buffer = Encoding.ASCII.GetBytes(sb.ToString());
            nStream.Write(buffer, 0, buffer.Length);
        }

        private HttpStatusCode ReceiveResponse(NetworkStream nStream)
        {
            var buffer = new byte[BufferSize];
            var responseBuilder = new StringBuilder();

            WaitData(nStream);

            do
            {
                int bytesRead = nStream.Read(buffer, 0, BufferSize);
                responseBuilder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
            } while (nStream.DataAvailable);

            string response = responseBuilder.ToString();

            if (response.Length == 0)
                throw NewProxyException(Constants.ProxyException_ReceivedEmptyResponse);

            // Выделяем строку статуса. Пример: HTTP/1.1 200 OK\r\n
            string strStatus = response.Substring(" ", Utility.NewLine);
            if (strStatus == null)
                throw NewProxyException(Constants.ProxyException_ReceivedWrongResponse);

            int simPos = strStatus.IndexOf(' ');
            if (simPos == -1)
                throw NewProxyException(Constants.ProxyException_ReceivedWrongResponse);

            string statusLine = strStatus.Substring(0, simPos);

            if (statusLine.Length == 0)
                throw NewProxyException(Constants.ProxyException_ReceivedWrongResponse);

            return Enum.TryParse(statusLine, out HttpStatusCode statusCode)
                ? statusCode
                : 0;
        }

        private void WaitData(NetworkStream nStream)
        {
            int sleepTime = 0;
            int delay = nStream.ReadTimeout < 10 ?
                10 : nStream.ReadTimeout;

            while (!nStream.DataAvailable)
            {
                if (sleepTime >= delay)
                    throw NewProxyException(Constants.ProxyException_WaitDataTimeout);

                sleepTime += 10;
                Thread.Sleep(10);
            }
        }

        #endregion
    }
}
