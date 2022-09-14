using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents a collection of strings that represent query options.
    /// </summary>
    public class RequestParams : List<KeyValuePair<string, string>>
    {
        /// <summary>
        /// Query by enumeration of parameters and their values.
        /// </summary>
        public string Query => Utility.ToQueryString(this, ValuesUnescaped, KeysUnescaped);

        /// <summary>
        /// Indicates whether to skip encoding query parameter values.
        /// </summary>
        public readonly bool ValuesUnescaped;

        /// <summary>
        /// Specifies whether to skip encoding query parameter names.
        /// </summary>
        public readonly bool KeysUnescaped;

        /// <inheritdoc />
        /// <param name="valuesUnescaped">Indicates whether to skip encoding query parameter values.</param>
        /// <param name="keysUnescaped">Specifies whether to skip encoding query parameter names.</param>
        public RequestParams(bool valuesUnescaped = false, bool keysUnescaped = false)
        {
            ValuesUnescaped = valuesUnescaped;
            KeysUnescaped = keysUnescaped;
        }

        /// <summary>
        /// Sets a new query parameter.
        /// </summary>
        /// <param name="paramName">The name of the request parameter.</param>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="paramName"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="paramName"/> is an empty string.</exception>
        public object this[string paramName]
        {
            set
            {
                #region Parameter check

                if (paramName == null)
                    throw new ArgumentNullException(nameof(paramName));

                if (paramName.Length == 0)
                    throw ExceptionHelper.EmptyString(nameof(paramName));

                #endregion

                string str = value?.ToString() ?? string.Empty;

                Add(new KeyValuePair<string, string>(paramName, str));
            }
        }
    }
}
