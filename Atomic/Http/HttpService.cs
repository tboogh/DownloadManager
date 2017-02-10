using System;
using System.Linq;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Atomic.Core.Managers;
using Atomic.Core.Storage;

namespace Atomic.Core.Http
{
    public class HttpService : IHttpService
    {
        private readonly IStorage _storage;
        private readonly HttpClient _httpClient;

        public HttpService(IStorage storage, HttpClient httpClient = null)
        {
            _storage = storage;
            _httpClient = httpClient ?? new HttpClient();
			_httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public IObservable<double> DownloadFileAsync(string url, string fileIdentifier)
        {
			return Observable.Create<double>(async observer => {
				CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
				
				HttpResponseMessage response = null;
				try {
					response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationTokenSource.Token);
				} catch (Exception e) {
					observer.OnError(e);
					return Disposable.Empty;
				}
	
				if (!response.IsSuccessStatusCode) {
					observer.OnError(new HttpStatusCodeException(response.StatusCode));
					return Disposable.Empty;
				}

				var total = response.Content.Headers.ContentLength ?? -1L;
				var canReportProgress = total != -1;
				observer.OnNext(0.0);
				using (var transientStream = _storage.GetTransientOutputStream(fileIdentifier)) {
					System.IO.Stream stream = null;
					try {
						stream = await response.Content.ReadAsStreamAsync();
					} catch (Exception e){
						stream.Dispose();
						observer.OnError(e);
						return Disposable.Empty;
					}
						
					long totalRead = 0L;
					byte[] buffer = new byte[4096];
					bool isMoreToRead = true;
	
					do {
						int readCount = 0;
						try {
							readCount = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationTokenSource.Token);
						} catch(Exception e){
							observer.OnError(e);
							break;
						}

						if (readCount == 0) {
							isMoreToRead = false;
						} else {
							byte[] data = new byte[readCount];
							buffer.ToList()
								.CopyTo(0, data, 0, readCount);
	
							transientStream.Write(data, 0, readCount);
	
							totalRead += readCount;
	
							if (canReportProgress) {
								double value = (totalRead / (double)total);
								observer.OnNext(value);
							}
						}
					} while (isMoreToRead);
					stream.Dispose();
				}
				observer.OnNext(1.0);
				observer.OnCompleted();
			    return new CancellationDisposable(cancellationTokenSource);
			});
        }
    }
}