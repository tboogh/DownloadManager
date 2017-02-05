using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Atomic.Core.Http
{
    public interface IHttpService
    {
        IObservable<double> DownloadFileAsync(string url, string fileIdentifier);
    }
}
