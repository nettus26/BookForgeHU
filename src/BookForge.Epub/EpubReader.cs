using BookForge.Core.Models;
using BookForge.Epub.Interfaces;

namespace BookForge.Epub;

public class EpubReader : IEpubReader
{
    private readonly EpubReaderV2 reader;

    public EpubReader()
    {
        reader = new EpubReaderV2();
    }

    public Book Load(string filePath)
    {
        return reader.Load(filePath);
    }
}