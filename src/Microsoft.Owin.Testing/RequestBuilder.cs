// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Owin.Testing
{
    /// <summary>
    /// Used to construct a HttpRequestMessage object.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "HttpRequestMessage is disposed by HttpClient in SendAsync")]
    public class RequestBuilder
    {
        private readonly TestServer _server;
        private readonly HttpRequestMessage _req;

        /// <summary>
        /// Construct a new HttpRequestMessage with the given path.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="path"></param>
        [SuppressMessage("Microsoft.Usage", "CA2234:PassSystemUriObjectsInsteadOfStrings", Justification = "Not a full URI")]
        public RequestBuilder(TestServer server, string path)
        {
            if (server == null)
            {
                throw new ArgumentNullException("server");
            }

            _server = server;
            _req = new HttpRequestMessage(HttpMethod.Get, path);
        }

        /// <summary>
        /// Configure any HttpRequestMessage properties.
        /// </summary>
        /// <param name="configure"></param>
        /// <returns></returns>
        public RequestBuilder And(Action<HttpRequestMessage> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException("configure");
            }

            configure(_req);
            return this;
        }

        /// <summary>
        /// Add the given header and value to the request or request content.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public RequestBuilder AddHeader(string name, string value)
        {
            if (!_req.Headers.TryAddWithoutValidation(name, value))
            {
                if (_req.Content == null)
                {
                    _req.Content = new StreamContent(Stream.Null);
                }
                if (!_req.Content.Headers.TryAddWithoutValidation(name, value))
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidHeaderName, name), "name");
                }
            }
            return this;
        }

        /// <summary>
        /// Set the request method and start processing the request.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public Task<HttpResponseMessage> SendAsync(string method)
        {
            _req.Method = new HttpMethod(method);
            return _server.HttpClient.SendAsync(_req);
        }
    }
}
