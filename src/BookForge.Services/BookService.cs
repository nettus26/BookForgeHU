using BookForge.Core.Models;
using BookForge.Services.Interfaces;

namespace BookForge.Services;

public class BookService : IBookService
{
    public Book CreateNewBook(string title, string author)
    {
        return new Book
        {
            Title = title,
            Author = author,
            CreatedDate = DateTime.Now
        };
    }

    public void SaveBook(Book book)
    {
        // Később ide kerül a mentési logika
    }

    public Book? LoadBook(string filePath)
    {
        // Később ide kerül az EPUB/DOCX betöltés
        return null;
    }
}