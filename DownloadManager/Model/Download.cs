using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DownloadManager.Core.Model
{
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