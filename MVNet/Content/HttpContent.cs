using System;
using System.IO;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents the request body. Released immediately upon dispatch.
    /// </summary>
    public abstract class HttpContent : IDisposable
    {
        /// <summary>
        /// MIME content type.
        /// </summary>
        protected string MimeContentType = string.Empty;


        /// <summary>
        /// Gets or sets the MIME content type.
        /// </summary>
        public string ContentType
        {
            get => MimeContentType;
            set => MimeContentType = value ?? string.Empty;
        }


        #region Methods (public)

        /// <summary>
        /// Counts and returns the length of the request body in bytes.
        /// </summary>
        /// <returns>The length of the request body in bytes.</returns>
        public abstract long CalculateContentLength();

        /// <summary>
        /// Writes the request body data to the stream.
        /// </summary>
        /// <param name="stream">The stream where the request body data will be written.</param>
        public abstract void WriteTo(Stream stream);

        /// <inheritdoc />
        /// <summary>
        /// Releases all resources used by the current instance of the class <see cref="T:MVNet.HttpContent" />.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion


        /// <summary>
        /// Releases the unmanaged (and optionally managed) resources used by the object <see cref="HttpContent"/>.
        /// </summary>
        /// <param name="disposing">Value <see langword="true"/> allows you to release managed and unmanaged resources; value <see langword="false"/> releases only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
