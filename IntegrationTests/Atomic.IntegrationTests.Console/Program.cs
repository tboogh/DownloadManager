using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Atomic.Core.Http;
using Atomic.Core.Managers;
using Atomic.Core.Storage;

namespace Atomic.IntegrationTests.Console
{
    public class LocalStorage : IStorage
    {
        public Stream GetTransientOutputStream(string fileIdentifier)
        {
            var path = "e:/Temp";
            FileStream file = File.Create(Path.Combine(path, fileIdentifier));
            return file;
        }

        public Stream GetFileStream(string fileIdentifier)
        {
            throw new NotImplementedException();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) => true;
            IStorage storage = new LocalStorage();
            IHttpService httpService = new HttpService(storage);
            var downloadManager = new DownloadManager(httpService);
            
            ((INotifyCollectionChanged) downloadManager.Downloads).CollectionChanged += (sender, eventArgs) =>
            {
                int count = downloadManager.Downloads.Count;
                System.Console.WriteLine($"Downloads: {count}");
                
            };
            
            for (int i = 0; i < 100; ++i)
            {
                var fileUrl = $"http://localhost:59901/api/values?size={(1024 * 1024 * 8)+i}";
                downloadManager.DownloadFile(fileUrl);
            }
            System.Console.ReadKey();
        }
    }
}
