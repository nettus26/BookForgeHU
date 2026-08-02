using System.IO.Compression;

namespace BookForge.Epub.Helpers;

public class EpubPackage
{
    public string FilePath { get; }

    public EpubPackage(string filePath)
    {
        FilePath = filePath;
    }

    public ZipArchive Open()
    {
        return ZipFile.OpenRead(FilePath);
    }
}