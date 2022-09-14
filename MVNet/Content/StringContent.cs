using System.Text;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents the request body as a string.
    /// </summary>
    public class StringContent : BytesContent
    {
        #region Constructors (public)

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.StringContent" />.
        /// </summary>
        /// <param name="content">Content content.</param>
        /// <exception cref="T:System.ArgumentNullException">Parameter value <paramref name="content" /> equals <see langword="null" />.</exception>
        /// <remarks>The default content type is 'text/plain'.</remarks>
        public StringContent(string content) : this(content, Encoding.UTF8)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.StringContent" />.
        /// </summary>
        /// <param name="content">Content content.</param>
        /// <param name="encoding">The encoding used to convert data into a sequence of bytes.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// Parameter value <paramref name="content" /> equals <see langword="null" />.
        /// -or-
        /// Parameter value <paramref name="encoding" /> equals <see langword="null" />.
        /// </exception>
        /// <remarks>The default content type is 'text/plain'.</remarks>
        public StringContent(string content, Encoding encoding)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            Content = encoding?.GetBytes(content) ?? throw new ArgumentNullException(nameof(encoding));
            Offset = 0;
            Count = Content.Length;

            MimeContentType = "text/plain";
        }

        #endregion
    }
}
