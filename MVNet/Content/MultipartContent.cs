using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MVNet
{
    /// <inheritdoc cref="HttpContent" />
    /// <summary>
    /// Represents the request body as multipart content.
    /// </summary>
    public class MultipartContent : HttpContent, IEnumerable<HttpContent>
    {
        private sealed class Element
        {
            #region Fields (public)

            public string Name;
            public string FileName;

            public HttpContent Content;

            #endregion


            public bool IsFieldFile()
            {
                return FileName != null;
            }
        }


        #region Constants (private)

        private const int FieldTemplateSize = 43;
        private const int FieldFileTemplateSize = 72;
        private const string FieldTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n";
        private const string FieldFileTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";

        #endregion

        #region Fields (private)

        private readonly string _boundary;
        private List<Element> _elements = new List<Element>();

        #endregion


        #region Constructors (public)

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.MultipartContent" />.
        /// </summary>
        public MultipartContent() : this("----------------" + GetRandomString(16))
        {
        }

        /// <summary>
        /// Initializes a new instance of the class <see cref="MultipartContent"/>.
        /// </summary>
        /// <param name="boundary">A border to separate the component parts of the content.</param>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="boundary"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="boundary"/> is an empty string.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Parameter value <paramref name="boundary"/> is over 70 characters long.</exception>
        public MultipartContent(string boundary)
        {
            #region Parameter Check

            if (boundary == null)
                throw new ArgumentNullException(nameof(boundary));

            if (boundary.Length == 0)
                throw ExceptionHelper.EmptyString(nameof(boundary));

            if (boundary.Length > 70)
                throw ExceptionHelper.CanNotBeGreater(nameof(boundary), 70);

            #endregion

            _boundary = boundary;

            MimeContentType = $"multipart/form-data; boundary={_boundary}";
        }

        #endregion


        #region Methods (public)

        /// <summary>
        /// Adds a new compound content element to the request body.
        /// </summary>
        /// <param name="content">The value of the element.</param>
        /// <param name="name">Element name.</param>
        /// <exception cref="System.ObjectDisposedException">The current instance has already been deleted.</exception>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="content"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="name"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="name"/> is an empty string.</exception>
        public void Add(HttpContent content, string name)
        {
            #region Parameter Check

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (name.Length == 0)
                throw ExceptionHelper.EmptyString(nameof(name));

            #endregion

            var element = new Element {
                Name = name,
                Content = content
            };

            _elements.Add(element);
        }

        /// <summary>
        /// Adds a new compound content element to the request body.
        /// </summary>
        /// <param name="content">The value of the element.</param>
        /// <param name="name">Element name.</param>
        /// <param name="fileName">The filename of the element.</param>
        /// <exception cref="System.ObjectDisposedException">The current instance has already been deleted.</exception>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="content"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="name"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="fileName"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="name"/> is an empty string.</exception>
        public void Add(HttpContent content, string name, string fileName)
        {
            #region Parameter Check

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (name.Length == 0)
                throw ExceptionHelper.EmptyString(nameof(name));

            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));

            #endregion

            content.ContentType = Utility.DetermineMediaType(Path.GetExtension(fileName));

            var element = new Element {
                Name = name,
                FileName = fileName,
                Content = content
            };

            _elements.Add(element);
        }

        /// <summary>
        /// Adds a new compound content element to the request body.
        /// </summary>
        /// <param name="content">The value of the element.</param>
        /// <param name="name">Element name.</param>
        /// <param name="fileName">The filename of the element.</param>
        /// <param name="contentType">MIME content type.</param>
        /// <exception cref="System.ObjectDisposedException">The current instance has already been deleted.</exception>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="content"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="name"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="fileName"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="name"/> is an empty string.</exception>
        public void Add(HttpContent content, string name, string fileName, string contentType)
        {
            #region Parameter Check

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (name.Length == 0)
                throw ExceptionHelper.EmptyString(nameof(name));

            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            content.ContentType = contentType;

            var element = new Element {
                Name = name,
                FileName = fileName,
                Content = content
            };

            _elements.Add(element);
        }

        /// <inheritdoc />
        /// <summary>
        /// Counts and returns the length of the request body in bytes.
        /// </summary>
        /// <returns>The length of the request body in bytes.</returns>
        /// <exception cref="T:System.ObjectDisposedException">The current instance has already been deleted.</exception>
        public override long CalculateContentLength()
        {
            ThrowIfDisposed();

            long length = 0;

            foreach (var element in _elements)
            {
                length += element.Content.CalculateContentLength();

                if (element.IsFieldFile())
                {
                    length += FieldFileTemplateSize;
                    length += element.Name.Length;
                    length += element.FileName.Length;
                    length += element.Content.ContentType.Length;
                }
                else
                {
                    length += FieldTemplateSize;
                    length += element.Name.Length;
                }

                // 2 (--) + x (boundary) + 2 (\r\n) ...item... + 2 (\r\n).
                length += _boundary.Length + 6;
            }

            // 2 (--) + x (boundary) + 2 (--) + 2 (\r\n).
            length += _boundary.Length + 6;

            return length;
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

            var newLineBytes = Encoding.ASCII.GetBytes("\r\n");
            var boundaryBytes = Encoding.ASCII.GetBytes("--" + _boundary + "\r\n");

            foreach (var element in _elements)
            {
                stream.Write(boundaryBytes, 0, boundaryBytes.Length);

                string field;

                if (element.IsFieldFile())
                {
                    field = string.Format(
                        FieldFileTemplate, element.Name, element.FileName, element.Content.ContentType);
                }
                else
                    field = string.Format(FieldTemplate, element.Name);

                var fieldBytes = Encoding.ASCII.GetBytes(field);
                stream.Write(fieldBytes, 0, fieldBytes.Length);

                element.Content.WriteTo(stream);
                stream.Write(newLineBytes, 0, newLineBytes.Length);
            }

            boundaryBytes = Encoding.ASCII.GetBytes("--" + _boundary + "--\r\n");
            stream.Write(boundaryBytes, 0, boundaryBytes.Length);
        }

        /// <inheritdoc />
        /// <summary>
        /// Returns an enumerator of composite content elements.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="T:System.ObjectDisposedException">The current instance has already been deleted.</exception>
        public IEnumerator<HttpContent> GetEnumerator()
        {
            ThrowIfDisposed();

            return _elements.Select(e => e.Content).GetEnumerator();
        }

        #endregion


        /// <inheritdoc />
        /// <summary>
        /// Releases the unmanaged (and optionally managed) resources used by the object <see cref="T:MVNet.HttpContent" />.
        /// </summary>
        /// <param name="disposing">Value <see langword="true" /> allows you to release managed and unmanaged resources; value <see langword="false" /> releases only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposing || _elements == null)
                return;

            foreach (var element in _elements)
                element.Content.Dispose();

            _elements = null;
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            ThrowIfDisposed();

            return GetEnumerator();
        }


        #region Methods (private)

        private static string GetRandomString(int length)
        {
            var random = new Random(Guid.NewGuid().GetHashCode());
            var strBuilder = new StringBuilder(length);
            for (int i = 0; i < length; ++i)
            {
                switch (random.Next(3))
                {
                    case 0:
                        strBuilder.Append((char)random.Next(48, 58));
                        break;

                    case 1:
                        strBuilder.Append((char)random.Next(97, 123));
                        break;

                    default:
                        strBuilder.Append((char)random.Next(65, 91));
                        break;
                }
            }

            return strBuilder.ToString();
        }

        private void ThrowIfDisposed()
        {
            if (_elements == null)
                throw new ObjectDisposedException("MultipartContent");
        }

        #endregion
    }
}