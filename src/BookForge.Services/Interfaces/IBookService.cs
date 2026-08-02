using BookForge.Core.Models;

namespace BookForge.Services.Interfaces;

public interface IBookService
{
    Book CreateNewBook(string title, string author);

    void SaveBook(Book book);

    Book? LoadBook(string filePath);
}