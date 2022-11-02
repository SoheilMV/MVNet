using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace MVNet
{
    public class AzadiProxyClient : ProxyClient
    {
        private readonly Security _security;
        private const int _defaultPort = 9898;
        private const int _buffersize = 4096;

        #region Constructors (public)

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.Socks5ProxyClient" /> the specified data about the proxy server.
        /// </summary>
        /// <param name="host">Proxy host.</param>
        /// <param name="port">Proxy port.</param>
        public AzadiProxyClient(string secret, string host, int port = _defaultPort) : this(secret, host, port, string.Empty, string.Empty)
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
        public AzadiProxyClient(string secret, string host, int port, string username, string password) : base(ProxyType.Azadi, host, port, username, password)
        {
            _security = new Security(secret);
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
                SendCommand(nStream, destinationHost, destinationPort);
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

        #region Static methods (public)

        /// <summary>
        /// Converts a string to an instance of a class <see cref="AzadiProxyClient"/>.
        /// </summary>
        /// <param name="proxyAddress">A string of the form - host:port:secret or host:port:username:password:secret or ap link.</param>
        /// <returns>Class instance <see cref="AzadiProxyClient"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="proxyAddress"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="proxyAddress"/> is an empty string.</exception>
        /// <exception cref="System.FormatException">The <paramref name="proxyAddress"/> format is incorrect.</exception>
        public new static AzadiProxyClient Parse(string proxyAddress)
        {
            if (proxyAddress.Contains("://"))
            {
                return ProxyClient.Parse(proxyAddress) as AzadiProxyClient;
            }
            else if (proxyAddress.Contains(":"))
            {
                return ProxyClient.Parse(ProxyType.Azadi, proxyAddress) as AzadiProxyClient;
            }
            else
                throw new FormatException(Constants.Azadi_FormatIsIncorrect);
        }

        /// <summary>
        /// Converts a string to an instance of a class <see cref="AzadiProxyClient"/>. Gets a value indicating whether the conversion was successful.
        /// </summary>
        /// <param name="proxyAddress">A string of the form - host:port:secret or host:port:username:password:secret or ap link.</param>
        /// <param name="result">If the conversion is successful, then contains an instance of the class <see cref="AzadiProxyClient"/>, otherwise <see langword="null"/>.</param>
        /// <returns>Value <see langword="true"/>, if parameter <paramref name="proxyAddress"/> converted successfully, otherwise <see langword="false"/>.</returns>
        public static bool TryParse(string proxyAddress, out AzadiProxyClient result)
        {
            if (proxyAddress.Contains("://"))
            {
                if (!ProxyClient.TryParse(proxyAddress, out var proxy))
                {
                    result = null;
                    return false;
                }

                result = proxy as AzadiProxyClient;
                return true;
            }
            else if(proxyAddress.Contains(":"))
            {
                if (!TryParse(ProxyType.Azadi, proxyAddress, out var proxy))
                {
                    result = null;
                    return false;
                }

                result = proxy as AzadiProxyClient;
                return true;
            }
            else
            {
                result = null;
                return false;
            }
        }

        #endregion

        #region Methods (private)

        private void SendCommand(NetworkStream stream, string destinationHost, int destinationPort)
        {
            if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
            {
                string[] request = new string[4] { Username, Password, destinationHost, destinationPort.ToString() };
                stream.Write(_security.Encrypt(request.ToByteArray()));

                byte[] response = new byte[_buffersize];
                int count = stream.Read(response, 0, response.Length);

                response = _security.Decrypt(response.Take(count).ToArray());

                AzadiError error = (AzadiError)response.ToInt32();

                if (error != AzadiError.None)
                    HandleCommandError(error);
            }
            else
            {
                string[] request = new string[2] { destinationHost, destinationPort.ToString() };
                stream.Write(_security.Encrypt(request.ToByteArray()));

                byte[] response = new byte[_buffersize];
                int count = stream.Read(response, 0, response.Length);

                response = _security.Decrypt(response.Take(count).ToArray());

                AzadiError error = (AzadiError)response.ToInt32();

                if (error != AzadiError.None)
                    HandleCommandError(error);
            }
        }

        private void HandleCommandError(AzadiError status)
        {
            string errorMessage;

            switch (status)
            {
                case AzadiError.Login:
                    errorMessage = Constants.Azadi_CommandReplyAuthWrong;
                    break;

                case AzadiError.Host:
                    errorMessage = Constants.Azadi_CommandReplyHostIncorrect;
                    break;

                case AzadiError.Remote:
                    errorMessage = Constants.Azadi_CommandReplyConnectionRefused;
                    break;

                case AzadiError.Unknown:
                    errorMessage = Constants.UnknownError;
                    break;

                default:
                    errorMessage = Constants.UnknownError;
                    break;
            }

            string exceptionMsg = string.Format(Constants.ProxyException_CommandError, errorMessage, ToString());

            throw NewProxyException(exceptionMsg);
        }

        #endregion

        private enum AzadiError : int
        {
            None = 1,
            Login = 2,
            Host = 3,
            Remote = 4,
            Unknown = 5
        }

        private class Security
        {
            ChaCha20Poly1305 chacha20 = null;
            private byte[] _nonce = new byte[12];

            public Security(string secret)
            {
                using (MD5 md5 = MD5.Create())
                {
                    var key = new Rfc2898DeriveBytes(secret, md5.ComputeHash(Encoding.UTF8.GetBytes(secret)), 1000);
                    chacha20 = new ChaCha20Poly1305(key.GetBytes(32));
                    _nonce = key.GetBytes(12);
                }
            }

            public byte[] Encrypt(byte[] input)
            {
                byte[] ciphertext = new byte[input.Length];
                byte[] tag = new byte[16];

                chacha20.Encrypt(_nonce, input, ciphertext, tag);

                using (var stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream, Encoding.UTF8))
                    {
                        writer.Write(tag);
                        writer.Write(ciphertext);
                        writer.Flush();
                    }
                    return stream.ToArray();
                }
            }

            public byte[] Decrypt(byte[] input)
            {
                using (var stream = new MemoryStream(input))
                {
                    using (var reader = new BinaryReader(stream, Encoding.UTF8))
                    {
                        var tag = reader.ReadBytes(16);
                        var ciphertext = reader.ReadBytes(input.Length - 16);
                        var plaintext = new byte[ciphertext.Length];

                        chacha20.Decrypt(_nonce, ciphertext, tag, plaintext);

                        return plaintext;
                    }
                }
            }
        }
    }
}
