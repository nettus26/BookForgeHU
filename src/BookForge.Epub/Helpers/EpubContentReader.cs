using System.IO.Compression;

namespace BookForge.Epub.Helpers;

public class EpubContentReader
{
    public string ReadEntry(ZipArchive archive, string path)
    {
        var entry = archive.Entries
            .FirstOrDefault(e =>
                e.FullName.Replace("\\", "/")
                .Equals(path, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
            throw new Exception($"Nem található fájl: {path}");

        using var reader = new StreamReader(entry.Open());

        return reader.ReadToEnd();
    }
}