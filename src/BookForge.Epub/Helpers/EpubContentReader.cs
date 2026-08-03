using System.IO.Compression;
using System.Linq;

namespace BookForge.Epub.Helpers;

public class EpubContentReader
{
    public string ReadEntry(ZipArchive archive, string path)
    {
        var normalizedPath = path.Replace("\\", "/");

        var entry = archive.Entries.FirstOrDefault(e =>
            e.FullName.Replace("\\", "/")
             .Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            throw new Exception(
                $"Nem található fájl: {path}\n\nA ZIP tartalma:\n" +
                string.Join("\n", archive.Entries.Select(e => e.FullName)));
        }

        using var reader = new StreamReader(entry.Open());

        return reader.ReadToEnd();
    }
}