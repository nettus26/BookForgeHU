using BookForge.Core.Models;
using BookForge.Epub;

namespace BookForge.Services;

public class ImportService
{
    private readonly EpubReader reader;
    private readonly LibraryService library;


    public ImportService()
    {
        reader = new EpubReader();
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