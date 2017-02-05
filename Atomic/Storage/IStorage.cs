using System.IO;

namespace Atomic.Core.Storage
{
    public interface IStorage
    {
        Stream GetTransientOutputStream(string fileIdentifier);

        Stream GetFileStream(string fileIdentifier);
    }
}
