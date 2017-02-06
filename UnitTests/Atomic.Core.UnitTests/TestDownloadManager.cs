using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Atomic.Core.Http;
using Atomic.Core.Managers;
using Atomic.Core.Model;
using Microsoft.Reactive.Testing;
using NSubstitute;

namespace Atomic.Core.UnitTests
{
    public class TestException : Exception
    {
    }

    [TestFixture]
    public class TestDownloadManager
    {
        [Test]
        public void DownloadFile_EnquesDownload()
        {
            IHttpService httpService = Substitute.For<IHttpService>();
            httpService.DownloadFileAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(Observable.Never(0.5));

            var downloadManager = new DownloadManager(httpService);
            string url = "http://www.someplace.com/file.bin";
            var download = downloadManager.DownloadFile(url);

            var queueDownload = downloadManager.Downloads.FirstOrDefault();

            Assert.AreSame(download, queueDownload);
        }

        [Test]
        public void DownloadFile_ReturnsExistingDownload()
        {
            IHttpService httpService = Substitute.For<IHttpService>();
            httpService.DownloadFileAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(Observable.Never(0.5));

            var downloadManager = new DownloadManager(httpService);
            string url = "http://www.someplace.com/file.bin";
            var download = downloadManager.DownloadFile(url);
            var secondDownload = downloadManager.DownloadFile(url);

            Assert.AreSame(download, secondDownload);
        }

        [Test]
        public void DownloadFile_DownloadRemovedOnCompletion()
        {
            TestScheduler testScheduler = new TestScheduler();
            ITestableObservable<double> testObservable = testScheduler.CreateColdObservable(new Recorded<Notification<double>>(1, Notification.CreateOnCompleted<double>()));

            IHttpService httpService = Substitute.For<IHttpService>();
            httpService.DownloadFileAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(testObservable);

            var downloadManager = new DownloadManager(httpService);
            string url = "http://www.someplace.com/file.bin";
            var download = downloadManager.DownloadFile(url);
            testScheduler.AdvanceBy(1);
            var queueDownload = downloadManager.Downloads.FirstOrDefault();

            Assert.IsNull(queueDownload);
        }

        [Test]
        public void DownloadFile_DownloadRemovedOnFailure()
        {
            TestScheduler testScheduler = new TestScheduler();
            ITestableObservable<double> testObservable = testScheduler.CreateColdObservable(new Recorded<Notification<double>>(1, Notification.CreateOnError<double>(new TestException())), 
																							new Recorded<Notification<double>>(2, Notification.CreateOnError<double>(new TestException())), 
																							new Recorded<Notification<double>>(3, Notification.CreateOnError<double>(new TestException())));
		
            IHttpService httpService = Substitute.For<IHttpService>();
            httpService.DownloadFileAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(testObservable);

            var downloadManager = new DownloadManager(httpService);
			downloadManager.MaxRetryCount = 3;
            string url = "http://www.someplace.com/file.bin";
            var download = downloadManager.DownloadFile(url);
            testScheduler.AdvanceBy(1);
            testScheduler.AdvanceBy(1);
            testScheduler.AdvanceBy(1);

            var queueDownload = downloadManager.Downloads.FirstOrDefault();

            Assert.IsNull(queueDownload);
        }

        [Test]
        public void DownloadFile_DoesNotRunMoreDownloadsThenNumberOfConcurrentDownloads()
        {
            TestScheduler testScheduler = new TestScheduler();
            ITestableObservable<double> testObservable = testScheduler.CreateColdObservable(new Recorded<Notification<double>>(1, Notification.CreateOnError<double>(new TestException())));

            IHttpService httpService = Substitute.For<IHttpService>();
            httpService.DownloadFileAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(testObservable);

            var downloadManager = new DownloadManager(httpService);
            for (int i = 0; i < downloadManager.NumberOfConcurrentDownloads + 1; ++i)
            {
                string url = $"http://www.someplace.com/file_{i}.bin";
                var download = downloadManager.DownloadFile(url);
            }

            var queuedDownload = downloadManager.Downloads[downloadManager.NumberOfConcurrentDownloads];

            Assert.AreEqual(DownloadStatus.Queued, queuedDownload.Status);
        }

        [Test]
        public void DownloadFile_ContinuesQueueDownloadCompletion()
        {
            TestScheduler testScheduler = new TestScheduler();
			ITestableObservable<double> testObservable = testScheduler.CreateColdObservable(new Recorded<Notification<double>>(1, Notification.CreateOnCompleted<double>()));
            ITestableObservable<double> testObservable2 = testScheduler.CreateColdObservable(new Recorded<Notification<double>>(1, Notification.CreateOnCompleted<double>()));
            IHttpService httpService = Substitute.For<IHttpService>();

            var downloadManager = new DownloadManager(httpService);
            for (int i = 0; i < downloadManager.NumberOfConcurrentDownloads + 1; ++i)
            {
                string url = $"http://www.someplace.com/file_{i}.bin";
                if (i < downloadManager.NumberOfConcurrentDownloads)
                {
                    httpService.DownloadFileAsync(url, Arg.Any<string>())
                        .Returns(testObservable);
                }
                else
                {
                    httpService.DownloadFileAsync(url, Arg.Any<string>())
                        .Returns(testObservable2);
                }

                var download = downloadManager.DownloadFile(url);
            }
            testScheduler.AdvanceBy(1);

            Assert.AreEqual(1, downloadManager.Downloads.Count);
        }

        [Test]
        public async Task DownloadFile_Download_ReportsCorrectProgressWhenContentLengthIsKnown()
        {
            TestScheduler testScheduler = new TestScheduler();
            ITestableObservable<double> testObservable = testScheduler.CreateColdObservable(new Recorded<Notification<double>>(1, Notification.CreateOnNext(0.1)), 
                new Recorded<Notification<double>>(2, Notification.CreateOnNext(0.2)), 
                new Recorded<Notification<double>>(3, Notification.CreateOnNext(0.3)),
            new Recorded<Notification<double>>(4, Notification.CreateOnNext(0.4)),
            new Recorded<Notification<double>>(5, Notification.CreateOnNext(0.5)));
            
			IHttpService httpService = Substitute.For<IHttpService>();
            httpService.DownloadFileAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(testObservable);

            var downloadManager = new DownloadManager(httpService);

            TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
            string url = $"http://www.someplace.com/file.bin";
            var download = downloadManager.DownloadFile(url);
            List<double> results = new List<double>();
            download.Progress.Subscribe(d =>
            {
                results.Add(d);
                if (results.Count == 5)
                {
                    taskCompletionSource.TrySetResult(true);
                }
            });
            testScheduler.AdvanceBy(5);
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            cancellationTokenSource.Token.Register(() =>
                { taskCompletionSource.TrySetCanceled(); });

            var result = await taskCompletionSource.Task;
            Assert.AreEqual(6, results.Count);
        }
        
        [Test]
        public void CancelDownload_WithUrl_RemovesDownload(){
        	IHttpService httpService = Substitute.For<IHttpService>();
            httpService.DownloadFileAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(Observable.Never(0.5));

            var downloadManager = new DownloadManager(httpService);

            string url = $"http://www.someplace.com/file.bin";
            var download = downloadManager.DownloadFile(url);

			downloadManager.CancelDownload(url);
			
			var removedDownload = downloadManager.Downloads.FirstOrDefault(x => x.Url == url);

			Assert.Null(removedDownload);
		}
		
		[Test]
        public void CancelDownload_WithDownloadObject_RemovesDownload(){
        	IHttpService httpService = Substitute.For<IHttpService>();
            httpService.DownloadFileAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(Observable.Never(0.5));

            var downloadManager = new DownloadManager(httpService);
	
            string url = $"http://www.someplace.com/file.bin";
            var download = downloadManager.DownloadFile(url);

			downloadManager.CancelDownload(download);
			
			var removedDownload = downloadManager.Downloads.FirstOrDefault(x => x.Url == url);

			Assert.Null(removedDownload);
		}
		
		[Test]
        public void CancelDownload_QeuedDownload_RemovesDownload(){
        	IHttpService httpService = Substitute.For<IHttpService>();
            httpService.DownloadFileAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(Observable.Never(0.5));

            var downloadManager = new DownloadManager(httpService);

			for (int i = 0; i < 5; ++i) {
				string fileUrl = $"http://www.someplace.com/file_{i}.bin";
				var download = downloadManager.DownloadFile(fileUrl);
			}

			var url = $"http://www.someplace.com/file_{5}.bin";
			downloadManager.CancelDownload(url);
			
			var removedDownload = downloadManager.Downloads.FirstOrDefault(x => x.Url == url);

			Assert.Null(removedDownload);
		}
    }
}