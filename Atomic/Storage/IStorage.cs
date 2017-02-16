using System.IO;

namespace Atomic.Core.Storage
{
    public interface IStorage
    {
        string GetDocumentsPath();
        Stream GetOutputStream(string fileIdentifier);

        Stream GetFileStream(string fileIdentifier);
    }
}
