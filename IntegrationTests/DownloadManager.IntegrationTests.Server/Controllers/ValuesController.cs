using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;

namespace DownloadManager.IntegrationTests.Server.Controllers
{
    public class ValuesController : ApiController
    {
        public HttpResponseMessage Get(int size)
        {
            byte[] byteData = new byte[size];
            Random random = new Random();
            random.NextBytes(byteData);

            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
            result.Content = new ByteArrayContent(byteData);
            result.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/octet-stream");

            return result;
        }
    }
}
