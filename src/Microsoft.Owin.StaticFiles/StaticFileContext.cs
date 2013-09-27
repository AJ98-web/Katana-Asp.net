// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles.Infrastructure;

namespace Microsoft.Owin.StaticFiles
{
    using SendFileFunc = Func<string, long, long?, CancellationToken, Task>;
    using System.Text;

    internal struct StaticFileContext
    {
        private readonly IOwinContext _context;
        private readonly StaticFileOptions _options;
        private readonly PathString _matchUrl;
        private readonly IOwinRequest _request;
        private readonly IOwinResponse _response;
        private string _method;
        private bool _isGet;
        private bool _isHead;
        private PathString _subPath;
        private string _contentType;
        private IFileInfo _fileInfo;
        private long _length;
        private DateTime _lastModified;
        private string _lastModifiedString;
        private string _etag;

        private PreconditionState _ifMatchState;
        private PreconditionState _ifNoneMatchState;
        private PreconditionState _ifModifiedSinceState;
        private PreconditionState _ifUnmodifiedSinceState;
        private PreconditionState _ifRangeState;
        private PreconditionState _rangeState;

        private IList<Tuple<long, long>> _ranges;

        public StaticFileContext(IOwinContext context, StaticFileOptions options, PathString matchUrl)
        {
            _context = context;
            _options = options;
            _matchUrl = matchUrl;
            _request = context.Request;
            _response = context.Response;

            _method = null;
            _isGet = false;
            _isHead = false;
            _contentType = null;
            _fileInfo = null;
            _length = 0;
            _lastModified = new DateTime();
            _etag = null;
            _lastModifiedString = null;
            _ifMatchState = PreconditionState.Unspecified;
            _ifNoneMatchState = PreconditionState.Unspecified;
            _ifModifiedSinceState = PreconditionState.Unspecified;
            _ifUnmodifiedSinceState = PreconditionState.Unspecified;
            _ifRangeState = PreconditionState.Unspecified;
            _rangeState = PreconditionState.Unspecified;
            _ranges = null;
        }

        internal enum PreconditionState
        {
            Unspecified,
            NotModified,
            PartialContent,
            ShouldProcess,
            PreconditionFailed,
            NotSatisfiable,
        }

        public bool IsHeadMethod
        {
            get { return _isHead; }
        }

        public bool ValidateMethod()
        {
            _method = _request.Method;
            _isGet = Helpers.IsGetMethod(_method);
            _isHead = Helpers.IsHeadMethod(_method);
            return _isGet || _isHead;
        }

        // Check if the URL matches any expected paths
        public bool ValidatePath()
        {
            return Helpers.TryMatchPath(_context, _matchUrl, forDirectory: false, subpath: out _subPath);
        }

        public bool LookupContentType()
        {
            if (_options.ContentTypeProvider.TryGetContentType(_subPath.Value, out _contentType))
            {
                return true;
            }

            if (_options.ServeUnknownFileTypes)
            {
                _contentType = _options.DefaultContentType;
                return true;
            }

            return false;
        }

        public bool LookupFileInfo()
        {
            bool found = _options.FileSystem.TryGetFileInfo(_subPath.Value, out _fileInfo);
            if (found)
            {
                _length = _fileInfo.Length;

                DateTime last = _fileInfo.LastModified;
                // Truncate to the second.
                _lastModified = new DateTime(last.Year, last.Month, last.Day, last.Hour, last.Minute, last.Second, last.Kind);
                _lastModifiedString = _lastModified.ToString(Constants.HttpDateFormat, CultureInfo.InvariantCulture);

                long etagHash = _lastModified.ToFileTimeUtc() ^ _length;
                _etag = '\"' + Convert.ToString(etagHash, 16) + "\"";
            }
            return found;
        }

        public void ComprehendRequestHeaders()
        {
            ComputeIfMatch();

            ComputeIfModifiedSince();

            ComputeRange();
        }

        private void ComputeIfMatch()
        {
            string etag = Helpers.RemoveQuotes(_etag);

            // 14.24 If-Match
            IList<string> ifMatch = _request.Headers.GetCommaSeparatedValues(Constants.IfMatch); // Removes quotes
            if (ifMatch != null)
            {
                _ifMatchState = PreconditionState.PreconditionFailed;
                foreach (var segment in ifMatch)
                {
                    if (segment.Equals("*", StringComparison.Ordinal)
                        || segment.Equals(etag, StringComparison.Ordinal))
                    {
                        _ifMatchState = PreconditionState.ShouldProcess;
                        break;
                    }
                }
            }

            // 14.26 If-None-Match
            IList<string> ifNoneMatch = _request.Headers.GetCommaSeparatedValues(Constants.IfNoneMatch);
            if (ifNoneMatch != null)
            {
                _ifNoneMatchState = PreconditionState.ShouldProcess;
                foreach (var segment in ifNoneMatch)
                {
                    if (segment.Equals("*", StringComparison.Ordinal)
                        || segment.Equals(etag, StringComparison.Ordinal))
                    {
                        _ifNoneMatchState = PreconditionState.NotModified;
                        break;
                    }
                }
            }
        }

        private void ComputeIfModifiedSince()
        {
            // 14.25 If-Modified-Since
            string ifModifiedSinceString = _request.Headers.Get(Constants.IfModifiedSince);
            DateTime ifModifiedSince;
            if (Helpers.TryParseHttpDate(ifModifiedSinceString, out ifModifiedSince))
            {
                bool modified = ifModifiedSince < _lastModified;
                _ifModifiedSinceState = modified ? PreconditionState.ShouldProcess : PreconditionState.NotModified;
            }

            // 14.28 If-Unmodified-Since
            string ifUnmodifiedSinceString = _request.Headers.Get(Constants.IfUnmodifiedSince);
            DateTime ifUnmodifiedSince;
            if (Helpers.TryParseHttpDate(ifUnmodifiedSinceString, out ifUnmodifiedSince))
            {
                bool unmodified = ifUnmodifiedSince >= _lastModified;
                _ifUnmodifiedSinceState = unmodified ? PreconditionState.ShouldProcess : PreconditionState.PreconditionFailed;
            }
        }

        private void ComputeRange()
        {
            // 14.35 Range
            // http://tools.ietf.org/html/draft-ietf-httpbis-p5-range-24
            // "Range is ignored when a conditional GET would result in a 304 (Not Modified) response."
            PreconditionState currentState = GetPreconditionState();
            string rangeHeader = _request.Headers.Get(Constants.Range);
            if (!string.IsNullOrEmpty(rangeHeader)
                && (currentState == PreconditionState.Unspecified || currentState == PreconditionState.ShouldProcess))
            {
                IList<Tuple<long?, long?>> ranges;
                if (RangeHelpers.TryParseRanges(rangeHeader, out ranges))
                {
                    ranges = RangeHelpers.GetSatisfiableRanges(ranges, _length);
                    IList<Tuple<long, long>> normalizedRanges = RangeHelpers.NormalizeRanges(ranges, _length);
                    if (normalizedRanges.Count == 0)
                    {
                        _rangeState = PreconditionState.NotSatisfiable;
                    }
                    else
                    {
                        _rangeState = PreconditionState.PartialContent;
                        _ranges = normalizedRanges;
                    }
                }
            }

            // 14.27 If-Range
            string ifRangeHeader = _request.Headers.Get(Constants.IfRange);
            // The If-Range header SHOULD only be used together with a Range header, and MUST be
            // ignored if the request does not include a [valid] Range header...
            if (!string.IsNullOrEmpty(ifRangeHeader) && _rangeState != PreconditionState.Unspecified)
            {
                DateTime ifRangeLastModified;
                if (Helpers.TryParseHttpDate(ifRangeHeader, out ifRangeLastModified))
                {
                    bool modified = _lastModified > ifRangeLastModified;
                    _ifRangeState = modified ? PreconditionState.ShouldProcess : PreconditionState.PartialContent;
                }
                else
                {
                    bool modified = !_etag.Equals(ifRangeHeader);
                    _ifRangeState = modified ? PreconditionState.ShouldProcess : PreconditionState.PartialContent;
                }

                // If the server receives a request (other than one including an If- Range request-header field)
                // with an unsatisfiable Range request- header field...it SHOULD return a response code of 416
                if (_rangeState == PreconditionState.NotSatisfiable && _ifRangeState == PreconditionState.PartialContent)
                {
                    _rangeState = PreconditionState.ShouldProcess;
                }
            }
        }

        public void ApplyResponseHeaders()
        {
            if (!string.IsNullOrEmpty(_contentType))
            {
                _response.ContentType = _contentType;
            }

            _response.Headers.Set(Constants.LastModified, _lastModifiedString);
            _response.ETag = _etag;
        }

        public PreconditionState GetPreconditionState()
        {
            return GetMaxPreconditionState(_ifMatchState, _ifNoneMatchState,
                _ifModifiedSinceState, _ifUnmodifiedSinceState,
                _ifRangeState, _rangeState);
        }

        private static PreconditionState GetMaxPreconditionState(params PreconditionState[] states)
        {
            PreconditionState max = PreconditionState.Unspecified;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] > max)
                {
                    max = states[i];
                }
            }
            return max;
        }

        public Task SendStatusAsync(int statusCode)
        {
            _response.StatusCode = statusCode;
            if (statusCode == Constants.Status200Ok)
            {
                _response.ContentLength = _length;
            }
            else if (statusCode == Constants.Status206PartialContent)
            {
                _response.StatusCode = Constants.Status206PartialContent;

                // Set Content-Range header & content-length
                Debug.Assert(_ranges != null && _ranges.Count > 0);
                if (_ranges.Count == 1)
                {
                    long start, length;
                    _response.Headers[Constants.ContentRange] = ComputeContentRange(_ranges[0], out start, out length);
                    _response.ContentLength = length;
                }
                else
                {
#if NET40
                    // Partial content with multiple ranges is not currently supported on 4.0.
                    _response.StatusCode = Constants.Status200Ok;
#else
                    Guid boundary = Guid.NewGuid();
                    _response.ContentType = "multipart/byteranges; boundary=" + boundary;
#endif
                }
            }
            else if (statusCode == Constants.Status416RangeNotSatisfiable)
            {
                // 14.16 Content-Range - A server sending a response with status code 416 (Requested range not satisfiable)
                // SHOULD include a Content-Range field with a byte-range-resp-spec of "*". The instance-length specifies
                // the current length of the selected resource.  e.g. */length
                _response.Headers[Constants.ContentRange] = "bytes */" + _length.ToString(CultureInfo.InvariantCulture);
            }
            return Constants.CompletedTask;
        }

        public Task SendAsync()
        {
            _response.StatusCode = Constants.Status200Ok;
            _response.ContentLength = _length;

            string physicalPath = _fileInfo.PhysicalPath;
            SendFileFunc sendFile = _response.Get<SendFileFunc>(Constants.SendFileAsyncKey);
            if (sendFile != null && !string.IsNullOrEmpty(physicalPath))
            {
                return sendFile(physicalPath, 0, _length, _request.CallCancelled);
            }

            Stream readStream = _fileInfo.CreateReadStream();
            var copyOperation = new StreamCopyOperation(readStream, _response.Body, _length, _request.CallCancelled);
            Task task = copyOperation.Start();
            task.ContinueWith(resultTask => readStream.Close(), TaskContinuationOptions.ExecuteSynchronously);
            return task;
        }

        // Note: This assumes ranges have been normalized to absolute byte offsets.
        private string ComputeContentRange(Tuple<long, long> range, out long start, out long length)
        {
            start = range.Item1;
            long end = range.Item2;
            length = end - start + 1;
            return string.Format(CultureInfo.InvariantCulture, "bytes {0}-{1}/{2}", start, end, _length);
        }

        internal Task SendRangesAsync()
        {
            Debug.Assert(_ranges != null && _ranges.Count > 0);
            if (_ranges.Count == 1)
            {
                return SendRangeAsync();
            }
#if NET40
            // TODO: Can the async/await loop be sanely back-ported?
            // Fall-back, just send the whole body.
            return SendAsync();
#else
            return SendMultipartRangesAsync();
#endif
        }

        // When there is only a single range the bytes are sent directly in the body.
        private Task SendRangeAsync()
        {
            _response.StatusCode = Constants.Status206PartialContent;

            long start, length;
            _response.Headers[Constants.ContentRange] = ComputeContentRange(_ranges[0], out start, out length);
            _response.ContentLength = length;

            string physicalPath = _fileInfo.PhysicalPath;
            SendFileFunc sendFile = _response.Get<SendFileFunc>(Constants.SendFileAsyncKey);
            if (sendFile != null && !string.IsNullOrEmpty(physicalPath))
            {
                return sendFile(physicalPath, start, length, _request.CallCancelled);
            }

            Stream readStream = _fileInfo.CreateReadStream();
            readStream.Seek(start, SeekOrigin.Begin); // TODO: What if !CanSeek?
            var copyOperation = new StreamCopyOperation(readStream, _response.Body, length, _request.CallCancelled);
            Task task = copyOperation.Start();
            task.ContinueWith(resultTask => readStream.Close(), TaskContinuationOptions.ExecuteSynchronously);
            return task;
        }

#if !NET40
        // When there are multiple ranges the bytes are sent as multipart/byteranges.
        private async Task SendMultipartRangesAsync()
        {
            _response.StatusCode = Constants.Status206PartialContent;

            Guid boundary = Guid.NewGuid();
            _response.ContentType = "multipart/byteranges; boundary=" + boundary;
            string boundaryString = "--" + boundary;
            // Assume buffered or Chunked, we don't want to compute the Content-Length.

            string physicalPath = _fileInfo.PhysicalPath;
            SendFileFunc sendFile = _response.Get<SendFileFunc>(Constants.SendFileAsyncKey);
            bool useSendFile = (sendFile != null && !string.IsNullOrEmpty(physicalPath));

            Stream readStream = null;
            if (!useSendFile)
            {
                readStream = _fileInfo.CreateReadStream();
            }

            try
            {
                for (int i = 0; i < _ranges.Count; i++)
                {
                    _request.CallCancelled.ThrowIfCancellationRequested();

                    long start, length;
                    string contentRange = ComputeContentRange(_ranges[i], out start, out length);

                    StringBuilder headers = new StringBuilder();
                    if (i != 0)
                    {
                        headers.Append("\r\n");
                    }
                    headers.Append(boundaryString);
                    headers.Append("\r\n");
                    if (!string.IsNullOrEmpty(_contentType))
                    {
                        headers.Append(Constants.ContentType);
                        headers.Append(": ");
                        headers.Append(_contentType);
                        headers.Append("\r\n");
                    }
                    headers.Append(Constants.ContentRange);
                    headers.Append(": ");
                    headers.Append(contentRange);
                    headers.Append("\r\n\r\n");

                    await _response.WriteAsync(headers.ToString());

                    if (useSendFile)
                    {
                        await _response.Body.FlushAsync();
                        await sendFile(physicalPath, start, length, _request.CallCancelled);
                    }
                    else
                    {
                        readStream.Seek(start, SeekOrigin.Begin); // TODO: What if !CanSeek?
                        await new StreamCopyOperation(readStream, _response.Body, length, _request.CallCancelled).Start();
                    }
                }
            }
            finally
            {
                if (readStream != null)
                {
                    readStream.Dispose();
                }
            }

            await _response.WriteAsync("\r\n" + boundaryString + "--\r\n\r\n");
        }
#endif
    }
}
