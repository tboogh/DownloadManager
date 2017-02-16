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
using Atomic.Core.Storage;

namespace Atomic.Core.Managers {

	public class DownloadManager : IDownloadManager {
	    private readonly IStorage _storage;
	    private readonly Subject<int> _progressSubject = new Subject<int>();
        private readonly Subject<IDownload> _downloadUpdateSubject = new Subject<IDownload>();
	    public IHttpService HttpService { get; }
		public ConcurrentDictionary<string, Tuple<IDisposable, IDownload>> CurrentDownloadDictionary { get; } = new ConcurrentDictionary<string, Tuple<IDisposable, IDownload>>();
		public ConcurrentQueue<IDownload> DownloadQueue { get; } = new ConcurrentQueue<IDownload>();
		private readonly List<IDownload> _downloads = new List<IDownload>();
		
		public DownloadManager(IHttpService httpService, IStorage storage) {
		    _storage = storage;
		    HttpService = httpService;

			NumberOfConcurrentDownloads = 4;
			MaxRetryCount = 3;
		}

		public int NumberOfConcurrentDownloads { get; set; }
		public int MaxRetryCount { get; set; }
		public IObservable<int> DownloadProgress => _progressSubject.AsObservable();
	    public IObservable<IDownload> DownloadUpdated => _downloadUpdateSubject.AsObservable();

        public IDownload DownloadFile(string url, string assetFilename = null) {
			Tuple<IDisposable, IDownload> currentDownload;
			if (CurrentDownloadDictionary.TryGetValue(url, out currentDownload)) {
				return currentDownload.Item2;
			}

			if (assetFilename != null) {
				var duplicateFile = DownloadQueue.FirstOrDefault(x => x.FileIdentifier == assetFilename);
				if (duplicateFile != null) {
					return duplicateFile;
				}
			}
			Download download = new Download(url, assetFilename ?? Guid.NewGuid().ToString());
            download.Status = DownloadStatus.Queued;
            Debug.WriteLine($"Enqueue: {download.FileIdentifier}");
			
			DownloadQueue.Enqueue(download);
            _downloadUpdateSubject.OnNext(download);
            StartQueue();

			return download;
		}

		public void StartQueue() {
			ProcessQueue();
		}

		public void ProcessQueue() {
			if (_downloads.Count >= NumberOfConcurrentDownloads)
				return;
				
			IDownload outDownload;
			if (!DownloadQueue.TryDequeue(out outDownload)) {
				return;
			}
			Debug.WriteLine($"Deque: {outDownload.FileIdentifier}");
			
			// If the download has been cancelled we skip it
			if (outDownload.Status == DownloadStatus.Cancelled)
				return;
			
			var download = (Download)outDownload;
			_downloads.Add(outDownload);
			download.Status = DownloadStatus.InProgress;
			Debug.WriteLine($"Download Start: {download.FileIdentifier}");
			TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();

			IObservable<double> downloadFileObservable = HttpService.DownloadFileAsync(download.Url, download.FileIdentifier, _storage.GetOutputStream(download.FileIdentifier));

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
                    _progressSubject.OnNext(DownloadQueue.Count + CurrentDownloadDictionary.Count);
                } else {
					Debug.WriteLine($"Download Error Qeueu: {download.FileIdentifier}");

                    // Download failed so we add it to queue again
                    download.Status = DownloadStatus.Queued;
                    _downloadUpdateSubject.OnNext(download);
                    DownloadQueue.Enqueue(download);
				}

                Tuple<IDisposable, IDownload> compeltedDownload;
                CurrentDownloadDictionary.TryRemove(download.Url, out compeltedDownload);
                _downloads.Remove(download);
				StartQueue();
			}, () => {
				Debug.WriteLine($"Download Complete: {download.FileIdentifier}");
				_downloads.Remove(download);
				download.Status = DownloadStatus.Complete;
                _downloadUpdateSubject.OnNext(download);
                
                taskCompletionSource.TrySetResult(true);

				download.ProgressSubject.OnCompleted();

			    Tuple<IDisposable, IDownload> compeltedDownload;
			    CurrentDownloadDictionary.TryRemove(download.Url, out compeltedDownload);

                _progressSubject.OnNext(DownloadQueue.Count + CurrentDownloadDictionary.Count);
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