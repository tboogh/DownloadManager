using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Atomic.Core.Http;
using Atomic.Core.Model;

namespace Atomic.Core.Managers {
	public interface IDownloadManager {
		int NumberOfConcurrentDownloads { get; set; }
		int MaxRetryCount { get; set; }

		IDownload DownloadFile(string url, string assetFilename  = null);
		void CancelDownload(string url);
		void CancelDownload(IDownload download);
		IObservable<int> DownloadProgress { get; }
        IObservable<IDownload> DownloadUpdated { get; }
    }	
}