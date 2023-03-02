using System.Text;
using System.Text.Json;

namespace FluentApi
{
    internal class FluentApiClient
    {
        /// <summary>
        /// Response wrapper.
        /// </summary>
        public HttpResponseMessage? ResponseMessage { get; private set; }

        /// <summary>
        /// Request wrapper.
        /// </summary>
        public HttpRequestMessage? RequestMessage { get; private set; }

        #region Request properties

        /// <summary>
        /// HTTP client.
        /// </summary>
        private HttpClient? Client { get; set; }

        /// <summary>
        /// Request headers.
        /// </summary>
        private Dictionary<string, object?>? Headers { get; set; }

        /// <summary>
        /// Request payload.
        /// </summary>
        private object? Payload { get; set; }

        /// <summary>
        /// Request payload content-type.
        /// </summary>
        private string? PayloadContentType { get; set; }

        /// <summary>
        /// Request payload content encoding.
        /// </summary>
        private Encoding? PayloadEncoding { get; set; }

        /// <summary>
        /// Request timeout.
        /// </summary>
        private TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Request URI.
        /// </summary>
        public Uri Uri { get; private set; }

        /// <summary>
        /// Request user-agent.
        /// </summary>
        private string? UserAgent { get; set; }

        #endregion

        #region Creators

        /// <summary>
        /// Create a new instance of the fluent API client.
        /// </summary>
        /// <param name="uri">Request URI.</param>
        public FluentApiClient(Uri uri)
        {
            this.Uri = uri;
        }

        /// <summary>
        /// Create a new instance of the fluent API client.
        /// </summary>
        /// <param name="uri">Request URI.</param>
        /// <returns>Fluent API client.</returns>
        public static FluentApiClient Create(Uri uri)
        {
            return new(uri);
        }

        #endregion

        #region Request manipulators

        /// <summary>
        /// Add header.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="value">Value.</param>
        public FluentApiClient AddHeader(string name, object? value)
        {
            this.Headers ??= new();
            this.Headers.TryAdd(name, value);

            return this;
        }

        /// <summary>
        /// Add payload.
        /// </summary>
        /// <param name="payload">Payload object.</param>
        /// <param name="contentType">Content type header value.</param>
        /// <param name="encoding">Content encoding.</param>
        public FluentApiClient AddPayload(
            object payload,
            string? contentType = null,
            Encoding? encoding = null)
        {
            this.Payload = payload;
            this.PayloadContentType = contentType;
            this.PayloadEncoding = encoding;

            return this;
        }

        /// <summary>
        /// Set the request timeout.
        /// </summary>
        /// <param name="timeout">Timeout.</param>
        public FluentApiClient SetTimeout(TimeSpan timeout)
        {
            this.Timeout = timeout;
            return this;
        }

        /// <summary>
        /// Set the request user-agent.
        /// </summary>
        /// <param name="userAgent">User-agent.</param>
        public FluentApiClient SetUserAgent(string userAgent)
        {
            this.UserAgent = userAgent;
            return this;
        }

        #endregion

        #region Request executors

        /// <summary>
        /// Execute the request.
        /// </summary>
        /// <param name="httpMethod">HTTP method.</param>
        /// <param name="ctoken">Cancellation token.</param>
        /// <returns>Byte-array.</returns>
        public async Task<byte[]?> ExecuteAsync(
            HttpMethod httpMethod,
            CancellationToken ctoken)
        {
            this.Client ??= new();
            this.RequestMessage = new(httpMethod, this.Uri);

            // Add headers.
            if (this.Headers?.Count > 0)
            {
                foreach (var (key, value) in this.Headers)
                {
                    this.RequestMessage.Headers.TryAddWithoutValidation(key, value?.ToString());
                }
            }

            // Add payload.
            if (this.Payload != null)
            {
                if (this.Payload is string str)
                {
                    this.RequestMessage.Content = new StringContent(
                        str,
                        this.PayloadEncoding,
                        this.PayloadContentType);
                }
                else if (this.Payload is byte[] bytes)
                {
                    this.RequestMessage.Content = new ByteArrayContent(bytes);
                    this.RequestMessage.Headers.TryAddWithoutValidation("Content-Type", this.PayloadContentType);
                }
                else
                {
                    using var jsonStream = new MemoryStream();

                    await JsonSerializer.SerializeAsync(
                        jsonStream,
                        this.Payload,
                        cancellationToken: ctoken);

                    this.RequestMessage.Content = new ByteArrayContent(jsonStream.ToArray());
                    this.RequestMessage.Headers.TryAddWithoutValidation("Content-Type", this.PayloadContentType);
                }
            }

            // Set timeout.
            if (this.Timeout.HasValue)
            {
                this.Client.Timeout = this.Timeout.Value;
            }

            // Set user agent.
            if (this.UserAgent != null)
            {
                this.Client
                    .DefaultRequestHeaders
                    .UserAgent
                    .ParseAdd(this.UserAgent);
            }

            // Perform request.
            this.ResponseMessage = await this.Client.SendAsync(
                this.RequestMessage,
                ctoken);

            // Get content.
            if (this.ResponseMessage.Content == null)
            {
                return null;
            }

            using var resStream = new MemoryStream();

            await this.ResponseMessage.Content.CopyToAsync(
                resStream,
                ctoken);

            return resStream.ToArray();
        }

        /// <summary>
        /// Execute the request.
        /// </summary>
        /// <typeparam name="T">Deserialize response body to type.</typeparam>
        /// <param name="httpMethod">HTTP method.</param>
        /// <param name="ctoken">Cancellation token.</param>
        /// <returns>Casted type.</returns>
        public async Task<T?> ExecuteAsync<T>(
            HttpMethod httpMethod,
            CancellationToken ctoken)
        {
            var bytes = await this.ExecuteAsync(httpMethod, ctoken);

            if (bytes == null ||
                bytes.Length == 0)
            {
                return default;
            }

            var obj = JsonSerializer.Deserialize<T>(
                bytes,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return obj;
        }

        #endregion

        #region Shorthand functions

        /// <summary>
        /// Perform a DELETE request.
        /// </summary>
        /// <typeparam name="T">Deserialize response body to type.</typeparam>
        /// <param name="ctoken">Cancellation token.</param>
        /// <returns>Casted type.</returns>
        public async Task<T?> DeleteAsync<T>(CancellationToken ctoken)
        {
            return await this.ExecuteAsync<T>(HttpMethod.Delete, ctoken);
        }

        /// <summary>
        /// Perform a GET request.
        /// </summary>
        /// <typeparam name="T">Deserialize response body to type.</typeparam>
        /// <param name="ctoken">Cancellation token.</param>
        /// <returns>Casted type.</returns>
        public async Task<T?> GetAsync<T>(CancellationToken ctoken)
        {
            return await this.ExecuteAsync<T>(HttpMethod.Get, ctoken);
        }

        /// <summary>
        /// Perform a PATCH request.
        /// </summary>
        /// <typeparam name="T">Deserialize response body to type.</typeparam>
        /// <param name="ctoken">Cancellation token.</param>
        /// <returns>Casted type.</returns>
        public async Task<T?> PatchAsync<T>(CancellationToken ctoken)
        {
            return await this.ExecuteAsync<T>(HttpMethod.Patch, ctoken);
        }

        /// <summary>
        /// Perform a POST request.
        /// </summary>
        /// <typeparam name="T">Deserialize response body to type.</typeparam>
        /// <param name="ctoken">Cancellation token.</param>
        /// <returns>Casted type.</returns>
        public async Task<T?> PostAsync<T>(CancellationToken ctoken)
        {
            return await this.ExecuteAsync<T>(HttpMethod.Post, ctoken);
        }

        /// <summary>
        /// Perform a PUT request.
        /// </summary>
        /// <typeparam name="T">Deserialize response body to type.</typeparam>
        /// <param name="ctoken">Cancellation token.</param>
        /// <returns>Casted type.</returns>
        public async Task<T?> PutAsync<T>(CancellationToken ctoken)
        {
            return await this.ExecuteAsync<T>(HttpMethod.Put, ctoken);
        }

        #endregion
    }
}