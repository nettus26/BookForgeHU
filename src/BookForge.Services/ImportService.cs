using BookForge.Core.Models;
using BookForge.Epub;
using BookForge.Epub.Interfaces;

namespace BookForge.Services;

public class ImportService
{
    private readonly IEpubReader reader;
    private readonly LibraryService library;


    public ImportService()
    {
     reader = new EpubReaderV2();
        library = new LibraryService();
    }


    public Book ImportEpub(string filePath)
    {
        var book = reader.Load(filePath);

        book.FilePath = filePath;

        library.AddBook(book);

        return book;
    }
}