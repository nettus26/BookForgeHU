using System.IO.Compression;

namespace BookForge.Epub.Helpers;

public class EpubPackage : IDisposable
{
    public string FilePath { get; }

    private ZipArchive? archive;

    public EpubPackage(string filePath)
    {
        FilePath = filePath;
    }

    public ZipArchive Open()
    {
        archive = ZipFile.OpenRead(FilePath);
        return archive;
    }

    public void Dispose()
    {
        archive?.Dispose();
    }
}