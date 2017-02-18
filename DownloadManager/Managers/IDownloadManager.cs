using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DownloadManager.Core.Http;
using DownloadManager.Core.Model;

namespace DownloadManager.Core.Managers {
	public interface IDownloadManager {
        /// <summary>
        /// Sets the max number of downloads to run concurrently. You will most likely need to increase the DefaultConnectionLimit of ServicePointManager since it defaults to 2 to get any effect (ServicePointManager.DefaultConnectionLimit = 10)
        /// </summary>
		int NumberOfConcurrentDownloads { get; set; }

        /// <summary>
        /// The manager will try do to download any failed download multiple times before calling it quits. Defaults to 3
        /// </summary>
		int MaxRetryCount { get; set; }

        /// <summary>
        /// Qeues a file and returns a IDownload object, if assetfilename is not entered it will create a file with guid as filename that can be read in the IDownload object
        /// </summary>
        /// <param name="url"></param>
		IDownload DownloadFile(string url, string assetFilename  = null);

        /// <summary>
        /// Cancels the download, if it is in progress it will be aborted.
        /// </summary>
        /// <param name="url"></param>
        void CancelDownload(string url);

        /// <summary>
        /// Cancels the download, if it is in progress it will be aborted.S
        /// </summary>
        /// <param name="download"></param>
		void CancelDownload(IDownload download);

        /// <summary>
        /// An observable with count of the current queue downloads and the current downloads in progress
        /// </summary>
		IObservable<int> DownloadProgress { get; }

        /// <summary>
        /// Will be called when a download changes status
        /// </summary>
        IObservable<IDownload> DownloadUpdated { get; }
    }	
}