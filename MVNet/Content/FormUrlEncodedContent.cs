using System.Text;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents the request body as request parameters.
    /// </summary>
    public class FormUrlEncodedContent : BytesContent
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.FormUrlEncodedContent" />.
        /// </summary>
        /// <param name="content">The content of the request body as request parameters.</param>
        /// <param name="valuesUnescaped">Indicates whether to skip encoding query parameter values.</param>
        /// <param name="keysUnescaped">Specifies whether to skip encoding query parameter names.</param>
        /// <exception cref="T:System.ArgumentNullException">Parameter value <paramref name="content" /> equals <see langword="null" />.</exception>
        /// <remarks>The default content type is 'application/x-www-form-urlencoded'.</remarks>
        public FormUrlEncodedContent(IEnumerable<KeyValuePair<string, string>> content, bool valuesUnescaped = false, bool keysUnescaped = false)
        {
            #region Parameter Check

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            #endregion

            Init(Utility.ToQueryString(content, valuesUnescaped, keysUnescaped));
        }

        public FormUrlEncodedContent(Parameters parameters)
        {
            #region Parameter Check

            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            #endregion

            Init(parameters.Query);
        }

        private void Init(string content)
        {
            Content = Encoding.ASCII.GetBytes(content);
            Offset = 0;
            Count = Content.Length;

            MimeContentType = "application/x-www-form-urlencoded";
        }
    }
}
