using System;
using System.IO;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents the request body as bytes.
    /// </summary>
    public class BytesContent : HttpContent
    {
        #region Fields (protected)

        /// <summary>
        /// The content of the request body.
        /// </summary>
        protected byte[] Content;
        /// <summary>
        /// The byte offset of the contents of the request body.
        /// </summary>
        protected int Offset;
        /// <summary>
        /// The number of bytes of content to send.
        /// </summary>
        protected int Count;

        #endregion


        #region Constructors (public)

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.BytesContent" />.
        /// </summary>
        /// <param name="content">The content of the request body.</param>
        /// <exception cref="T:System.ArgumentNullException">Parameter value <paramref name="content" /> equals <see langword="null" />.</exception>
        /// <remarks>The default content type is - 'application/octet-stream'.</remarks>
        public BytesContent(byte[] content) : this(content, 0, content.Length)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class <see cref="BytesContent"/>.
        /// </summary>
        /// <param name="content">The content of the request body.</param>
        /// <param name="offset">The byte offset for the content.</param>
        /// <param name="count">The number of bytes sent from the content.</param>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="content"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Parameter value <paramref name="offset"/> less than 0.
        /// -or-
        /// Parameter value <paramref name="offset"/> greater than the length of the content.
        /// -or-
        /// Parameter value <paramref name="count"/> less than 0.
        /// -or-
        /// Parameter value <paramref name="count"/> greater than (content length - offset).</exception>
        /// <remarks>The default content type is - 'application/octet-stream'.</remarks>
        public BytesContent(byte[] content, int offset, int count)
        {
            #region Parameter Check

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            if (offset < 0)
                throw ExceptionHelper.CanNotBeLess(nameof(offset), 0);

            if (offset > content.Length)
                throw ExceptionHelper.CanNotBeGreater(nameof(offset), content.Length);

            if (count < 0)
                throw ExceptionHelper.CanNotBeLess(nameof(count), 0);

            if (count > content.Length - offset)
                throw ExceptionHelper.CanNotBeGreater(nameof(count), content.Length - offset);

            #endregion

            Content = content;
            Offset = offset;
            Count = count;

            MimeContentType = "application/octet-stream";
        }

        #endregion


        /// <summary>
        /// Initializes a new instance of the class <see cref="BytesContent"/>.
        /// </summary>
        protected BytesContent()
        {
        }


        #region Methods (public)

        /// <inheritdoc />
        /// <summary>
        /// Counts and returns the length of the request body in bytes.
        /// </summary>
        /// <returns>The length of the request body in bytes.</returns>
        public override long CalculateContentLength()
        {
            return Content.LongLength;
        }

        /// <inheritdoc />
        /// <summary>
        /// Writes the request body data to the stream.
        /// </summary>
        /// <param name="stream">The stream where the request body data will be written.</param>
        public override void WriteTo(Stream stream)
        {
            stream.Write(Content, Offset, Count);
        }

        #endregion
    }
}
