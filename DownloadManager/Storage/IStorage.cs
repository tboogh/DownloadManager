using System.IO;

namespace DownloadManager.Core.Storage
{
    public interface IStorage
    {
        string GetDocumentsPath();
        Stream GetOutputStream(string fileIdentifier);

        Stream GetFileStream(string fileIdentifier);
    }
}
