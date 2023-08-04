using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace MVNet
{
    /// <summary>
    /// A client that provides proxies connections via AZADI proxies.
    /// </summary>
    public class AzadiProxyClient : ProxyClient
    {
        private readonly Security _security;
        private const int _buffersize = 4096;

        /// <summary>
        /// Creates an Azadi proxy client given the proxy <paramref name="settings"/>.
        /// </summary>
        public AzadiProxyClient(string secret, ProxySettings settings) : base(settings)
        {
            _security = new Security(secret);
        }

        protected async override Task CreateConnectionAsync(TcpClient client, string destinationHost, int destinationPort, CancellationToken cancellationToken = default)
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

            try
            {
                var nStream = client.GetStream();
                await SendCommand(nStream, destinationHost, destinationPort).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                client.Close();

                if (ex is IOException || ex is SocketException)
                {
                    throw new ProxyException("Error while working with proxy", ex);
                }

                throw;
            }
        }

        #region Methods (private)

        private async Task SendCommand(NetworkStream stream, string destinationHost, int destinationPort)
        {
            NetworkCredential? credentials = Settings?.Credentials;
            if (!string.IsNullOrEmpty(credentials?.UserName) && !string.IsNullOrEmpty(credentials?.Password))
            {
                string[] request = new string[4] { credentials.UserName!, credentials.Password!, destinationHost, destinationPort.ToString() };
                await stream.WriteAsync(_security.Encrypt(request.ToByteArray())).ConfigureAwait(false);

                byte[] response = new byte[_buffersize];
                int count = await stream.ReadAsync(response, 0, response.Length).ConfigureAwait(false);

                response = _security.Decrypt(response.Take(count).ToArray());

                AzadiError error = (AzadiError)response.ToInt32();

                if (error != AzadiError.None)
                    HandleCommandError(error);
            }
            else
            {
                string[] request = new string[2] { destinationHost, destinationPort.ToString() };
                await stream.WriteAsync(_security.Encrypt(request.ToByteArray())).ConfigureAwait(false);

                byte[] response = new byte[_buffersize];
                int count = await stream.ReadAsync(response, 0, response.Length).ConfigureAwait(false);

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
                    errorMessage = "Invalid username or password.";
                    break;

                case AzadiError.Host:
                    errorMessage = "The host entered is incorrect.";
                    break;

                case AzadiError.Remote:
                    errorMessage = "Connection refused.";
                    break;

                case AzadiError.Unknown:
                    errorMessage = "Unknown error.";
                    break;

                default:
                    errorMessage = "Unknown error.";
                    break;
            }

            string exceptionMsg = string.Format("{0} The proxy server '{1}'.", errorMessage, $"{Settings?.Host}:{Settings?.Port}");

            throw new ProxyException(exceptionMsg);
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
            ChaCha20Poly1305 chacha20;
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

    static internal class AzadiExtensions
    {
        internal static byte[] ToByteArray(this string[] input)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    var rows = input.GetLength(0);
                    writer.Write(rows);
                    for (int i = 0; i < rows; i++)
                    {
                        writer.Write(input[i]);
                    }
                    writer.Flush();
                    return stream.ToArray();
                }
            }
        }

        internal static string[] ToStringArray(this byte[] input)
        {
            using (var stream = new MemoryStream(input))
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    var rows = reader.ReadInt32();
                    var result = new string[rows];
                    for (int i = 0; i < rows; i++)
                    {
                        result[i] = reader.ReadString();
                    }
                    return result;
                }
            }
        }

        internal static byte[] ToByteArray(this int input)
        {
            return BitConverter.GetBytes(input);
        }

        internal static int ToInt32(this byte[] input)
        {
            return BitConverter.ToInt32(input, 0);
        }
    }
}
