using System;
using System.Net;

namespace Atomic.Core.Http
{
    public class HttpStatusCodeException : Exception
    {
        public HttpStatusCodeException(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; }
    }
}