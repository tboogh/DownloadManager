using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Atomic.Core.Http;
using Atomic.Core.Model;

namespace Atomic.Core.Managers
{
    public interface IDownloadManager
    {
        IDownload DownloadFile(string url);
        ReadOnlyObservableCollection<IDownload> Downloads { get; }
        int NumberOfConcurrentDownloads { get; set; }
    }

    public class DownloadManager : IDownloadManager
    {
        private readonly ObservableCollection<IDownload> _downloads;
        private Task _queueTask;
        public IHttpService HttpService { get; }
        public ConcurrentDictionary<string, Tuple<IDisposable, IDownload>> DownloadDictionary { get; } = new ConcurrentDictionary<string, Tuple<IDisposable, IDownload>>();
        public ConcurrentQueue<IDownload> DownloadQueue { get; } = new ConcurrentQueue<IDownload>();
        
        public DownloadManager(IHttpService httpService)
        {
            HttpService = httpService;
            _downloads = new ObservableCollection<IDownload>();
            Downloads = new ReadOnlyObservableCollection<IDownload>(_downloads);
            NumberOfConcurrentDownloads = 4;
        }

        public ReadOnlyObservableCollection<IDownload> Downloads { get; }
        public int NumberOfConcurrentDownloads { get; set; }

        public IDownload DownloadFile(string url)
        {
            Tuple<IDisposable, IDownload> currentDownload;
            if (DownloadDictionary.TryGetValue(url, out currentDownload))
            {
                return currentDownload.Item2;
            }

            Guid guid = Guid.NewGuid();
            Download download = new Download(url, guid.ToString());
            _downloads.Add(download);
            DownloadQueue.Enqueue(download);

            StartQueue();

            return download;
        }

        public void StartQueue()
        {
            if (_queueTask?.IsCompleted ?? true)
            {
                _queueTask = ProcessQueue();
            }
        }

        public async Task ProcessQueue()
        {
            do
            {
                IDownload outDownload;
                if (!DownloadQueue.TryDequeue(out outDownload))
                {
                    break;
                }

                var download = (Download) outDownload;
                download.Status = DownloadStatus.InProgress;

                TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();

                IObservable<double> downloadFileObservable = HttpService.DownloadFileAsync(download.Url, download.FileIdentifier);
                
                IDisposable disposable = downloadFileObservable.Subscribe(d =>
                {
                    download.ProgressSubject.OnNext(d);
                }, exception =>
                {
                    _downloads.Remove(download);
                    download.Status = DownloadStatus.Failed;
                    download.ErrorCount++;

                    // Download failed so we add it to queue again
                    DownloadQueue.Enqueue(download);
                    
                    taskCompletionSource.TrySetResult(true);

                    // Maybe not?
                    download.ProgressSubject.OnError(exception);
                }, () =>
                {
                    _downloads.Remove(download);
                    download.Status = DownloadStatus.Complete;
                    taskCompletionSource.TrySetResult(true);

                    download.ProgressSubject.OnCompleted();
                });
                if (!DownloadDictionary.TryAdd(download.Url, new Tuple<IDisposable, IDownload>(disposable, download)))
                {
                    throw new Exception("Could not add download to dictionary");
                }

                while (Downloads.Count(x => x.Status == DownloadStatus.InProgress) >= NumberOfConcurrentDownloads)
                {
                    await taskCompletionSource.Task;
                }
            }
            while (true);
        }
    }
}