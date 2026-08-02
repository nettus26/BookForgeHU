using System.IO.Compression;

namespace BookForge.Epub.Helpers;

public class EpubContentReader
{
    public string ReadEntry(ZipArchive archive, string path)
    {
       var entry = archive.GetEntry(path) 
            ?? archive.GetEntry(path.Replace("/", "\\"));

        if (entry == null)
            throw new Exception($"Nem található fájl: {path}");

        using var reader = new StreamReader(entry.Open());

        return reader.ReadToEnd();
    }
}