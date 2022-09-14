using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents the base implementation of a class for working with a proxy server.
    /// </summary>
    public abstract class ProxyClient : IEquatable<ProxyClient>
    {
        #region Fields (protected)

        /// <summary>
        /// Proxy type.
        /// </summary>
        protected ProxyType _type;

        /// <summary>
        /// Username for authorization on the proxy server.
        /// </summary>
        protected string _username;

        /// <summary>
        /// Password for authorization on the proxy server.
        /// </summary>
        protected string _password;

        /// <summary>
        /// Timeout in milliseconds when connecting to a proxy server.
        /// </summary>
        private int _connectTimeout = 9 * 1000; // 9 Seconds

        /// <summary>
        /// Timeout in milliseconds when writing to or reading from the stream.
        /// </summary>
        private int _readWriteTimeout = 30 * 1000; // 30 Seconds

        #endregion


        #region Properties (public)

        /// <summary>
        /// Returns the type of the proxy.
        /// </summary>
        public ProxyType Type => _type;

        /// <summary>
        /// Proxy host.
        /// </summary>
        /// <value>The default value is <see langword="null"/>.</value>
        public string Host { get; }

        /// <summary>
        /// Proxy port.
        /// </summary>
        /// <value>The default value is 1.</value>
        public int Port { get; } = 1;

        /// <summary>
        /// Gets or sets the username for authorization on the proxy server.
        /// </summary>
        /// <value>The default value is <see langword="null"/>.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">The parameter value is longer than 255 characters.</exception>
        public string Username
        {
            get => _username;
            set {
                #region Parameter Check

                if (value != null && value.Length > 255)
                {
                    throw new ArgumentOutOfRangeException(nameof(Username), string.Format(Constants.ArgumentOutOfRangeException_StringLengthCanNotBeMore, 255));
                }

                #endregion

                _username = value;
            }
        }

        /// <summary>
        /// Gets or sets the password for authorization on the proxy server.
        /// </summary>
        /// <value>The default value is <see langword="null"/>.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">The parameter value is longer than 255 characters.</exception>
        public string Password
        {
            get => _password;
            set {
                #region Parameter Check

                if (value != null && value.Length > 255)
                {
                    throw new ArgumentOutOfRangeException(nameof(Password), string.Format(Constants.ArgumentOutOfRangeException_StringLengthCanNotBeMore, 255));
                }

                #endregion

                _password = value;
            }
        }

        /// <summary>
        /// Gets or sets the timeout in milliseconds when connecting to a proxy server.
        /// </summary>
        /// <value>The default value is 9000ms, which is 9 seconds.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">The parameter value is less than 0.</exception>
        public int ConnectTimeout
        {
            get => _connectTimeout;
            set {
                #region Parameter check

                if (value < 0)
                    throw ExceptionHelper.CanNotBeLess("ConnectTimeout", 0);

                #endregion

                _connectTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the timeout, in milliseconds, when writing to or reading from the stream.
        /// </summary>
        /// <value>The default value is 30,000ms, which is 30 seconds.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">The parameter value is less than 0.</exception>
        public int ReadWriteTimeout
        {
            get => _readWriteTimeout;
            set {
                #region Parameter check

                if (value < 0)
                    throw ExceptionHelper.CanNotBeLess(nameof(ReadWriteTimeout), 0);

                #endregion

                _readWriteTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to specify the full address of the resource in the proxy-specific request header.
        /// If given <see langword="true"/> (default) - if the proxy is set correctly, use the absolute address in the request header.
        /// If given <see langword="false"/> - the relative address in the request header will always be used.
        /// </summary>
        public bool AbsoluteUriInStartingLine { get; set; }

        #endregion


        #region Constructors (protected)

        /// <summary>
        /// Initializes a new instance of the class <see cref="ProxyClient"/>.
        /// </summary>
        /// <param name="proxyType">Proxy type.</param>
        protected internal ProxyClient(ProxyType proxyType)
        {
            _type = proxyType;
        }

        /// <summary>
        /// Initializes a new instance of the class <see cref="ProxyClient"/>.
        /// </summary>
        /// <param name="proxyType">Proxy type.</param>
        /// <param name="address">Proxy host.</param>
        /// <param name="port">Proxy port.</param>
        protected internal ProxyClient(ProxyType proxyType, string address, int port)
        {
            _type = proxyType;
            Host = address;
            Port = port;
        }

        /// <summary>
        /// Initializes a new instance of the class <see cref="ProxyClient"/>.
        /// </summary>
        /// <param name="proxyType">Proxy type.</param>
        /// <param name="address">Proxy host.</param>
        /// <param name="port">Proxy port.</param>
        /// <param name="username">Username for authorization on the proxy server.</param>
        /// <param name="password">Password for authorization on the proxy server.</param>
        protected internal ProxyClient(ProxyType proxyType, string address, int port, string username, string password)
        {
            _type = proxyType;
            Host = address;
            Port = port;
            _username = username;
            _password = password;
        }

        #endregion


        #region Static Properties (Protected)

        /// <summary>
        /// HTTPS proxy server for debugging (Charles / Fiddler).
        /// The default address is 127.0.0.1:8888.
        /// </summary>
        public static HttpProxyClient DebugHttpProxy {
            get {
                if (_debugHttpProxy != null)
                    return _debugHttpProxy;

                _debugHttpProxy = HttpProxyClient.Parse("127.0.0.1:8888");
                return _debugHttpProxy;
            }
        }
        private static HttpProxyClient _debugHttpProxy;

        /// <summary>
        /// SOCKS5 proxy server for debugging (Charles / Fiddler).
        /// The default address is 127.0.0.1:8889.
        /// </summary>
        public static Socks5ProxyClient DebugSocksProxy => _debugSocksProxy ?? (_debugSocksProxy = Socks5ProxyClient.Parse("127.0.0.1:8889"));
        private static Socks5ProxyClient _debugSocksProxy;

        #endregion


        #region Static Methods

        /// <summary>
        /// Used to convert string proxies to a ProxyClient object.
        /// </summary>
        public static readonly Dictionary<string, ProxyType> ProxyProtocol = new Dictionary<string, ProxyType>
        {
            {"http", ProxyType.HTTP},
            {"https", ProxyType.HTTP},
            {"socks4", ProxyType.Socks4},
            {"socks4a", ProxyType.Socks4A},
            {"socks5", ProxyType.Socks5},
            {"socks", ProxyType.Socks5}
        };

        /// <summary>
        /// Converts a string to an instance of the proxy client class inherited from <see cref="ProxyClient"/>.
        /// </summary>
        /// <param name="proxyType">Proxy type.</param>
        /// <param name="proxyAddress">A string of the form - host:port:user_name:password. The last three parameters are optional.</param>
        /// <returns>An instance of the proxy client class, inherited from <see cref="ProxyClient"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="proxyAddress"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="proxyAddress"/> is an empty string.</exception>
        /// <exception cref="System.FormatException">The port format is incorrect.</exception>
        /// <exception cref="System.InvalidOperationException">An unsupported proxy type was received.</exception>
        public static ProxyClient Parse(ProxyType proxyType, string proxyAddress)
        {
            #region Parameter Check

            if (proxyAddress == null)
                throw new ArgumentNullException(nameof(proxyAddress));

            if (proxyAddress.Length == 0)
                throw ExceptionHelper.EmptyString(nameof(proxyAddress));

            #endregion

            var values = proxyAddress.Split(':');

            int port = 0;
            string host = values[0];

            if (values.Length >= 2)
            {
                #region Getting a port

                try
                {
                    port = int.Parse(values[1]);
                }
                catch (Exception ex)
                {
                    if (ex is FormatException || ex is OverflowException)
                    {
                        throw new FormatException(Constants.InvalidOperationException_ProxyClient_WrongPort, ex);
                    }

                    throw;
                }

                if (!ExceptionHelper.ValidateTcpPort(port))
                {
                    throw new FormatException(Constants.InvalidOperationException_ProxyClient_WrongPort);
                }

                #endregion
            }

            string username = null;
            string password = null;

            if (values.Length >= 3)
                username = values[2];

            if (values.Length >= 4)
                password = values[3];

            return ProxyHelper.CreateProxyClient(proxyType, host, port, username, password);
        }

        /// <inheritdoc cref="Parse(MVNet.ProxyType,string)"/>
        /// <param name="protoProxyAddress">String of the form - protocol://host:port:user_name:password. The last three parameters are optional.</param>
        /// <returns>An instance of the proxy client class, inherited from <see cref="ProxyClient"/>.</returns>
        public static ProxyClient Parse(string protoProxyAddress)
        {
            var proxy = protoProxyAddress.Split(new[] {"://"}, StringSplitOptions.RemoveEmptyEntries);
            if (proxy.Length < 2)
                return null;

            string proto = proxy[0];
            if (!ProxyProtocol.ContainsKey(proto))
                return null;

            var proxyType = ProxyProtocol[proto];
            return Parse(proxyType, proxy[1]);
        }

        /// <summary>
        /// Converts a string to an instance of the proxy client class inherited from <see cref="ProxyClient"/>. Gets a value indicating whether the conversion was successful.
        /// </summary>
        /// <param name="proxyType">Proxy type.</param>
        /// <param name="proxyAddress">A string of the form - host:port:user_name:password. The last three parameters are optional.</param>
        /// <param name="result">If the conversion is successful, then contains an instance of the proxy client class inherited from <see cref="ProxyClient"/>, otherwise <see langword="null"/>.</param>
        /// <returns>Value <see langword="true"/>, if parameter <paramref name="proxyAddress"/> converted successfully, otherwise <see langword="false"/>.</returns>
        public static bool TryParse(ProxyType proxyType, string proxyAddress, out ProxyClient result)
        {
            result = null;

            #region Parameter Check

            if (string.IsNullOrEmpty(proxyAddress))
                return false;

            #endregion

            var values = proxyAddress.Split(':');

            int port = 0;
            string host = values[0];

            if (values.Length >= 2 &&
                (!int.TryParse(values[1], out port) || !ExceptionHelper.ValidateTcpPort(port)))
            {
                return false;
            }

            string username = null;
            string password = null;

            if (values.Length >= 3)
                username = values[2];

            if (values.Length >= 4)
                password = values[3];

            try
            {
                result = ProxyHelper.CreateProxyClient(proxyType, host, port, username, password);
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc cref="TryParse(MVNet.ProxyType,string,out MVNet.ProxyClient)"/>
        /// <param name="protoProxyAddress">String of the form - protocol://host:port:user_name:password. The last three parameters are optional.</param>
        /// <param name="result">The result is an abstract proxy client</param>
        /// <returns>Value <see langword="true"/>, if parameter <paramref name="protoProxyAddress"/> converted successfully, otherwise <see langword="false"/>.</returns>
        public static bool TryParse(string protoProxyAddress, out ProxyClient result)
        {
            var proxy = protoProxyAddress.Split(new[] {"://"}, StringSplitOptions.RemoveEmptyEntries);
            if (proxy.Length < 2 || !ProxyProtocol.ContainsKey(proxy[0]))
            {
                result = null;
                return false;
            }

            var proxyType = ProxyProtocol[proxy[0]];
            return TryParse(proxyType, proxy[1], out result);
        }

        #endregion


        /// <summary>
        /// Creates a connection to the server through a proxy server.
        /// </summary>
        /// <param name="destinationHost">The destination host to contact through the proxy.</param>
        /// <param name="destinationPort">The port of the destination to contact through the proxy server.</param>
        /// <param name="tcpClient">Connection through which to work, or value <see langword="null"/>.</param>
        /// <returns>Connecting to a proxy server.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// Property value <see cref="Host"/> equals <see langword="null"/> or has zero length.
        /// -or-
        /// Property value <see cref="Port"/> less than 1 or greater than 65535.
        /// -or-
        /// Property value <see cref="Username"/> is longer than 255 characters.
        /// -or-
        /// Property value <see cref="Password"/> is longer than 255 characters.
        /// </exception>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="destinationHost"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="destinationHost"/> is an empty string.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Parameter value <paramref name="destinationPort"/> less than 1 or greater than 65535.</exception>
        /// <exception cref="MVNet.ProxyException">An error occurred while working with a proxy server.</exception>
        public abstract TcpClient CreateConnection(string destinationHost, int destinationPort, TcpClient tcpClient = null);


        #region Methods (public)

        /// <summary>
        /// Generates a host:port string representing the address of the proxy server.
        /// </summary>
        /// <returns>A string of the form host:port representing the address of the proxy server.</returns>
        public override string ToString() => $"{Host}:{Port}";

        /// <summary>
        /// Forms a string like - host:port:user_name:password. The last two parameters are added if they are given.
        /// </summary>
        /// <returns>A string of the form - host:port:user_name:password.</returns>
        public string ToExtendedString()
        {
            var strBuilder = new StringBuilder();

            strBuilder.AppendFormat("{0}:{1}", Host, Port);

            if (string.IsNullOrEmpty(_username))
                return strBuilder.ToString();

            strBuilder.AppendFormat(":{0}", _username);

            if (!string.IsNullOrEmpty(_password))
                strBuilder.AppendFormat(":{0}", _password);

            return strBuilder.ToString();
        }

        /// <summary>
        /// Returns the hash code for this proxy client.
        /// </summary>
        /// <returns>Hash code, specified as a 32-bit signed integer.</returns>
        public override int GetHashCode()
        {
            if (string.IsNullOrEmpty(Host))
                return 0;

            return Host.GetHashCode() ^ Port;
        }

        /// <inheritdoc/>
        /// <summary>
        /// Determines if two proxy clients are equal.
        /// </summary>
        /// <param name="proxy">The proxy client to compare against this instance.</param>
        /// <returns>Value <see langword="true"/>, if the two proxy clients are equal, otherwise the value <see langword="false"/>.</returns>
        public bool Equals(ProxyClient proxy)
        {
            if (proxy == null || Host == null)
                return false;

            return Host.Equals(proxy.Host, StringComparison.OrdinalIgnoreCase) && Port == proxy.Port;
        }

        /// <summary>
        /// Determines if two proxy clients are equal.
        /// </summary>
        /// <param name="obj">The proxy client to compare against this instance.</param>
        /// <returns>Value <see langword="true"/>, if the two proxy clients are equal, otherwise the value <see langword="false"/>.</returns>
        public override bool Equals(object obj) => obj is ProxyClient proxy && Equals(proxy);

        #endregion


        #region Methods (protected)

        /// <summary>
        /// Creates a connection to a proxy server.
        /// </summary>
        /// <returns>Connecting to a proxy server.</returns>
        /// <exception cref="MVNet.ProxyException">An error occurred while working with a proxy server.</exception>
        protected TcpClient CreateConnectionToProxy()
        {
            #region Create a connection

            var tcpClient = new TcpClient();
            Exception connectException = null;
            var connectDoneEvent = new ManualResetEventSlim();

            try
            {
                tcpClient.BeginConnect(Host, Port, ar => {
                    if (tcpClient.Client == null)
                        return;

                    try
                    {
                        tcpClient.EndConnect(ar);
                    }
                    catch (Exception ex)
                    {
                        connectException = ex;
                    }

                    connectDoneEvent.Set();
                }, tcpClient);
            }

            #region Catch's

            catch (Exception ex)
            {
                tcpClient.Close();

                if (ex is SocketException || ex is SecurityException)
                    throw NewProxyException(Constants.ProxyException_FailedConnect, ex);

                throw;
            }

            #endregion

            if (!connectDoneEvent.Wait(_connectTimeout))
            {
                tcpClient.Close();
                throw NewProxyException(Constants.ProxyException_ConnectTimeout);
            }

            if (connectException != null)
            {
                tcpClient.Close();

                if (connectException is SocketException)
                    throw NewProxyException(Constants.ProxyException_FailedConnect, connectException);

                throw connectException;
            }

            if (!tcpClient.Connected)
            {
                tcpClient.Close();
                throw NewProxyException(Constants.ProxyException_FailedConnect);
            }

            #endregion

            tcpClient.SendTimeout = _readWriteTimeout;
            tcpClient.ReceiveTimeout = _readWriteTimeout;

            return tcpClient;
        }

        /// <summary>
        /// Checks various proxy client settings for erroneous values.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Property value <see cref="Host"/> equals <see langword="null"/> or has zero length.</exception>
        /// <exception cref="System.InvalidOperationException">Property value <see cref="Port"/> less than 1 or greater than 65535.</exception>
        /// <exception cref="System.InvalidOperationException">Property value <see cref="Username"/> is longer than 255 characters.</exception>
        /// <exception cref="System.InvalidOperationException">Property value <see cref="Password"/> is longer than 255 characters.</exception>
        protected void CheckState()
        {
            if (string.IsNullOrEmpty(Host))
            {
                throw new InvalidOperationException(Constants.InvalidOperationException_ProxyClient_WrongHost);
            }

            if (!ExceptionHelper.ValidateTcpPort(Port))
            {
                throw new InvalidOperationException(Constants.InvalidOperationException_ProxyClient_WrongPort);
            }

            if (_username != null && _username.Length > 255)
            {
                throw new InvalidOperationException(Constants.InvalidOperationException_ProxyClient_WrongUsername);
            }

            if (_password != null && _password.Length > 255)
            {
                throw new InvalidOperationException(Constants.InvalidOperationException_ProxyClient_WrongPassword);
            }
        }

        /// <summary>
        /// Creates a proxy exception object.
        /// </summary>
        /// <param name="message">Error message explaining the reason for the exception.</param>
        /// <param name="innerException">The exception that threw the current exception, or the value <see langword="null"/>.</param>
        /// <returns>The proxy exception object.</returns>
        protected ProxyException NewProxyException(string message, Exception innerException = null)
        {
            return new ProxyException(string.Format(message, ToString()), this, innerException);
        }

        #endregion
    }
}
