using System.Text.Json;
using BookForge.Core.Models;

namespace BookForge.Services;

public class LibraryService
{
    private readonly string libraryFile =
        "bookforge-library.json";

    public List<Book> GetBooks()
    {
        if (!File.Exists(libraryFile))
            return new List<Book>();

        var json = File.ReadAllText(libraryFile);

        return JsonSerializer.Deserialize<List<Book>>(json)
               ?? new List<Book>();
    }

    public void AddBook(Book book)
    {
        var books = GetBooks();

        books.Add(book);

        Save(books);
    }

    public void RemoveBook(Book book)
    {
        var books = GetBooks();

        books.Remove(book);

        Save(books);
    }

    private void Save(List<Book> books)
    {
        var json = JsonSerializer.Serialize(
            books,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(
            libraryFile,
            json);
    }
}