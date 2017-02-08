using System.IO;

namespace Atomic.Core.Storage
{
    public interface IStorage
    {
        string GetDocumentsPath();
        Stream GetTransientOutputStream(string fileIdentifier);

        Stream GetFileStream(string fileIdentifier);
    }
}
