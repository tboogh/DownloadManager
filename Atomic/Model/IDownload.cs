using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace Atomic.Core.Model
{
    public enum DownloadStatus
    {
        Queued,
        InProgress,
        Complete,
        Failed
    }

    public interface IDownload
    {
        IObservable<double> Progress { get; }
        string Url { get; }
        string FileIdentifier { get; }
        DownloadStatus Status { get; }
        int ErrorCount { get; }
    }

    public class Download : IDownload
    {
        public Download(string url, string fileIdentifier)
        {
            Status = DownloadStatus.Queued;
            FileIdentifier = fileIdentifier;
            Url = url;
            ErrorCount = 0;
        }

        internal Subject<double> ProgressSubject { get; } = new Subject<double>();

        public IObservable<double> Progress => ProgressSubject.AsObservable().StartWith(0);
        public string FileIdentifier { get; }
        public string Url { get; }
        public DownloadStatus Status { get; internal set; }
        public int ErrorCount { get; internal set; }
    }
}
