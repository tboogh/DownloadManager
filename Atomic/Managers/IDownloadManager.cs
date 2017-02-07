using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Atomic.Core.Http;
using Atomic.Core.Model;

namespace Atomic.Core.Managers {
	public interface IDownloadManager {
		ReadOnlyObservableCollection<IDownload> Downloads { get; }
		int NumberOfConcurrentDownloads { get; set; }
		int MaxRetryCount { get; set; }

		IDownload DownloadFile(string url);
		void CancelDownload(string url);
		void CancelDownload(IDownload download);
		IObservable<int> DownloadProgress { get; }
	}

	public class DownloadManager : IDownloadManager {
		private readonly ObservableCollection<IDownload> _downloads;
		private readonly Subject<int> _progressSubject = new Subject<int>();
		private Task _queueTask;

		public IHttpService HttpService { get; }
		public ConcurrentDictionary<string, Tuple<IDisposable, IDownload>> CurrentDownloadDictionary { get; } = new ConcurrentDictionary<string, Tuple<IDisposable, IDownload>>();
		public ConcurrentQueue<IDownload> DownloadQueue { get; } = new ConcurrentQueue<IDownload>();

		public DownloadManager(IHttpService httpService) {
			HttpService = httpService;
			_downloads = new ObservableCollection<IDownload>();
			Downloads = new ReadOnlyObservableCollection<IDownload>(_downloads);
			NumberOfConcurrentDownloads = 4;
			MaxRetryCount = 3;
		}

		public ReadOnlyObservableCollection<IDownload> Downloads { get; }
		public int NumberOfConcurrentDownloads { get; set; }
		public int MaxRetryCount { get; set; }
		public IObservable<int> DownloadProgress => _progressSubject.AsObservable();
		
		public IDownload DownloadFile(string url) {
			Tuple<IDisposable, IDownload> currentDownload;
			if (CurrentDownloadDictionary.TryGetValue(url, out currentDownload)) {
				return currentDownload.Item2;
			}

			Guid guid = Guid.NewGuid();
			Download download = new Download(url, guid.ToString());
			_downloads.Add(download);
			DownloadQueue.Enqueue(download);

			StartQueue();

			return download;
		}

		public void StartQueue() {
			if (_queueTask?.IsCompleted ?? true) {
				_queueTask = ProcessQueue();
			}
		}

		public async Task ProcessQueue() {
			do {
				IDownload outDownload;
				if (!DownloadQueue.TryDequeue(out outDownload)) {
					break;
				}

				// If the download has been cancelled we skip it
				if (outDownload.Status == DownloadStatus.Cancelled)
					continue;

				var download = (Download)outDownload;
				download.Status = DownloadStatus.InProgress;

				TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();

				IObservable<double> downloadFileObservable = HttpService.DownloadFileAsync(download.Url, download.FileIdentifier);

				IDisposable disposable = downloadFileObservable.Subscribe(d => {
					download.ProgressSubject.OnNext(d);
				}, exception => {
					download.ErrorCount++;

					if (download.ErrorCount >= 3) {
						_downloads.Remove(download);
						download.Status = DownloadStatus.Failed;
						download.ProgressSubject.OnError(exception);
						_progressSubject.OnNext(_downloads.Count);
					} else {
						// Download failed so we add it to queue again
						DownloadQueue.Enqueue(download);
						StartQueue();
					}

					taskCompletionSource.TrySetResult(true);
				}, () => {
					_downloads.Remove(download);
					download.Status = DownloadStatus.Complete;
					taskCompletionSource.TrySetResult(true);

					download.ProgressSubject.OnCompleted();
					_progressSubject.OnNext(_downloads.Count);
					StartQueue();
				});

				if (!CurrentDownloadDictionary.TryAdd(download.Url, new Tuple<IDisposable, IDownload>(disposable, download))) {
					throw new Exception("Could not add download to dictionary");
				}

				while (Downloads.Count(x => x.Status == DownloadStatus.InProgress) >= NumberOfConcurrentDownloads) {
					await taskCompletionSource.Task;
				}
			}
			while (true);
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