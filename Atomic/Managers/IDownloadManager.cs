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

	public class DownloadManager : IDownloadManager {
		
		private readonly Subject<int> _progressSubject = new Subject<int>();
        private readonly Subject<IDownload> _downloadUpdateSubject = new Subject<IDownload>();
        private Task _queueTask;

		public IHttpService HttpService { get; }
		public ConcurrentDictionary<string, Tuple<IDisposable, IDownload>> CurrentDownloadDictionary { get; } = new ConcurrentDictionary<string, Tuple<IDisposable, IDownload>>();
		public ConcurrentQueue<IDownload> DownloadQueue { get; } = new ConcurrentQueue<IDownload>();
		private List<IDownload> _downloads = new List<IDownload>();
		
		public DownloadManager(IHttpService httpService) {
			HttpService = httpService;

			NumberOfConcurrentDownloads = 4;
			MaxRetryCount = 3;
		}

		public ReadOnlyObservableCollection<IDownload> Downloads { get; }
		public int NumberOfConcurrentDownloads { get; set; }
		public int MaxRetryCount { get; set; }
		public IObservable<int> DownloadProgress => _progressSubject.AsObservable();
	    public IObservable<IDownload> DownloadUpdated => _downloadUpdateSubject.AsObservable();

        public IDownload DownloadFile(string url, string assetFilename = null) {
			Tuple<IDisposable, IDownload> currentDownload;
			if (CurrentDownloadDictionary.TryGetValue(url, out currentDownload)) {
				return currentDownload.Item2;
			}

			Download download = new Download(url, assetFilename ?? Guid.NewGuid().ToString());
            download.Status = DownloadStatus.Queued;
			DownloadQueue.Enqueue(download);
            _downloadUpdateSubject.OnNext(download);
            StartQueue();

			return download;
		}

		public void StartQueue() {
			//if (_queueTask?.IsCompleted ?? true) {
				
			//	_queueTask = ProcessQueue();
			//}
			ProcessQueue();
		}

		public void ProcessQueue() {
			if (_downloads.Count >= NumberOfConcurrentDownloads)
				return;
				
			IDownload outDownload;
			if (!DownloadQueue.TryDequeue(out outDownload)) {
				return;
			}

			// If the download has been cancelled we skip it
			if (outDownload.Status == DownloadStatus.Cancelled)
				return;
			
			var download = (Download)outDownload;
			_downloads.Add(outDownload);
			download.Status = DownloadStatus.InProgress;
			Debug.WriteLine($"Download Start: {download.FileIdentifier}");
			TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();

			IObservable<double> downloadFileObservable = HttpService.DownloadFileAsync(download.Url, download.FileIdentifier);

			IDisposable disposable = downloadFileObservable.Subscribe(d => {
				download.ProgressSubject.OnNext(d);
			}, exception => {
				Debug.WriteLine($"Download Error: {download.FileIdentifier}");
				download.ErrorCount++;

				if (download.ErrorCount >= 3) {
					Debug.WriteLine($"Download Error Remove: {download.FileIdentifier}");
					
					download.Status = DownloadStatus.Failed;
                    _downloadUpdateSubject.OnNext(download);

                    download.ProgressSubject.OnError(exception);
					_progressSubject.OnNext(_downloads.Count);
				} else {
					Debug.WriteLine($"Download Error Qeueu: {download.FileIdentifier}");

                    // Download failed so we add it to queue again
                    download.Status = DownloadStatus.Queued;
                    _downloadUpdateSubject.OnNext(download);
                    DownloadQueue.Enqueue(download);
				}
				_downloads.Remove(download);
				StartQueue();
			}, () => {
				Debug.WriteLine($"Download Complete: {download.FileIdentifier}");
				_downloads.Remove(download);
				download.Status = DownloadStatus.Complete;
                _downloadUpdateSubject.OnNext(download);
                
                taskCompletionSource.TrySetResult(true);

				download.ProgressSubject.OnCompleted();
				_progressSubject.OnNext(_downloads.Count);
				StartQueue();
			});

			if (!CurrentDownloadDictionary.TryAdd(download.Url, new Tuple<IDisposable, IDownload>(disposable, download))) {
				throw new Exception("Could not add download to dictionary");
			}

			//while (Downloads.Count(x => x.Status == DownloadStatus.InProgress) >= NumberOfConcurrentDownloads) {
			//	Debug.WriteLine("Start wait");
			//	await taskCompletionSource.Task;
			//	Debug.WriteLine("End wait");
			//}
			StartQueue();
		}

		public void CancelDownload(string url) {
			Tuple<IDisposable, IDownload> currentDownload;
			if (CurrentDownloadDictionary.TryGetValue(url, out currentDownload)) {
				currentDownload.Item1.Dispose();
			}

			var downloads = _downloads.Where(x => x.Url == url).ToList();
			foreach (var download in downloads) {
				((Download)download).Status = DownloadStatus.Cancelled;
				_downloads.Remove(download);
			}
		}

		public void CancelDownload(IDownload download) {
			CancelDownload(download.Url);
		}
	}
}