using System;
using System.IO;

namespace DownloadManager.Core.Http
{
    public interface IHttpService
    {
        IObservable<double> DownloadFileAsync(string url, string fileIdentifier, Stream outputStream);
    }
}
