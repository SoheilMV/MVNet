using System;
using System.IO;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents the request body as a stream.
    /// </summary>
    public class StreamContent : HttpContent
    {
        #region Fields (shielded by electromagnetic radiation)

        /// <summary>
        /// The content of the request body.
        /// </summary>
        protected Stream ContentStream;
        /// <summary>
        /// The buffer size in bytes for the stream.
        /// </summary>
        protected int BufferSize;
        /// <summary>
        /// The byte position at which to start reading data from the stream.
        /// </summary>
        protected long InitialStreamPosition;

        #endregion


        /// <summary>
        /// Initializes a new instance of the class <see cref="StreamContent"/>.
        /// </summary>
        /// <param name="contentStream">The content of the request body.</param>
        /// <param name="bufferSize">The buffer size in bytes for the stream.</param>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="contentStream"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Flow <paramref name="contentStream"/> does not support reading or moving position.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"> Parameter value <paramref name="bufferSize"/> less than 1.</exception>
        /// <remarks>The default content type is 'application/octet-stream'.</remarks>
        public StreamContent(Stream contentStream, int bufferSize = 32768)
        {
            #region Parameter Check

            if (contentStream == null)
                throw new ArgumentNullException(nameof(contentStream));

            if (!contentStream.CanRead || !contentStream.CanSeek)
                throw new ArgumentException(Constants.ArgumentException_CanNotReadOrSeek, nameof(contentStream));

            if (bufferSize < 1)
                throw ExceptionHelper.CanNotBeLess(nameof(bufferSize), 1);

            #endregion

            ContentStream = contentStream;
            BufferSize = bufferSize;
            InitialStreamPosition = ContentStream.Position;

            MimeContentType = "application/octet-stream";
        }


        /// <summary>
        /// Initializes a new instance of the class <see cref="StreamContent"/>.
        /// </summary>
        protected StreamContent()
        {
        }


        #region Methods (public)

        /// <inheritdoc />
        /// <summary>
        /// Counts and returns the length of the request body in bytes.
        /// </summary>
        /// <returns>Content length in bytes.</returns>
        /// <exception cref="T:System.ObjectDisposedException">The current instance has already been deleted.</exception>
        public override long CalculateContentLength()
        {
            ThrowIfDisposed();

            return ContentStream.Length;
        }

        /// <inheritdoc />
        /// <summary>
        /// Writes the request body data to the stream.
        /// </summary>
        /// <param name="stream">The stream where the request body data will be written.</param>
        /// <exception cref="T:System.ObjectDisposedException">The current instance has already been deleted.</exception>
        /// <exception cref="T:System.ArgumentNullException">Parameter value <paramref name="stream" /> equals <see langword="null" />.</exception>
        public override void WriteTo(Stream stream)
        {
            ThrowIfDisposed();

            #region Parameter Check

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            #endregion

            ContentStream.Position = InitialStreamPosition;

            var buffer = new byte[BufferSize];

            while (true)
            {
                int bytesRead = ContentStream.Read(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                    break;

                stream.Write(buffer, 0, bytesRead);
            }
        }

        #endregion


        /// <inheritdoc />
        /// <summary>
        /// Releases the unmanaged (and optionally managed) resources used by the object <see cref="T:MVNet.HttpContent" />.
        /// </summary>
        /// <param name="disposing">Value <see langword="true" /> allows you to release managed and unmanaged resources; value <see langword="false" /> releases only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposing || ContentStream == null)
                return;

            ContentStream.Dispose();
            ContentStream = null;
        }


        private void ThrowIfDisposed()
        {
            if (ContentStream == null)
                throw new ObjectDisposedException("StreamContent");
        }
    }
}
