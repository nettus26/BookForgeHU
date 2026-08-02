using BookForge.Core.Models;
using BookForge.Epub.Interfaces;

namespace BookForge.Epub;

public class EpubReader : IEpubReader
{
    public Book Load(string filePath)
    {
        return new Book
        {
            Title = "Imported EPUB",
            Author = "Unknown"
        };
    }
}