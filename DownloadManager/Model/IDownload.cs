using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DownloadManager.Core.Model
{
    public enum DownloadStatus
    {
        Queued,
        InProgress,
        Complete,
        Failed,
        Cancelled
    }

    public interface IDownload
    {
        /// <summary>
        /// Updates when the download is in progress, value from 0.0 to 1.0.
        /// </summary>
        IObservable<double> Progress { get; }

        /// <summary>
        /// The url for the source of the download
        /// </summary>
        string Url { get; }

        /// <summary>
        /// The filename entered or a random guid
        /// </summary>
        string FileIdentifier { get; }

        /// <summary>
        /// The current status of the download
        /// </summary>
        DownloadStatus Status { get; }

        /// <summary>
        /// The numbers of failed attempts to download the file
        /// </summary>
        int ErrorCount { get; }
    }
}
