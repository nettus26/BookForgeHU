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

        // Duplikáció ellenőrzés
        var exists = books.Any(b =>
            b.Title == book.Title &&
            b.Author == book.Author);

        if (exists)
            return;

        books.Add(book);

        Save(books);
    }


    public Book? FindBook(string title)
    {
        var books = GetBooks();

        return books.FirstOrDefault(b =>
            b.Title.Contains(
                title,
                StringComparison.OrdinalIgnoreCase));
    }


    public void RemoveBook(Book book)
    {
        var books = GetBooks();

        var existing = books.FirstOrDefault(b =>
            b.Title == book.Title &&
            b.Author == book.Author);

        if (existing == null)
            return;

        books.Remove(existing);

        Save(books);
    }


    public void UpdateLastOpened(Book book)
    {
        var books = GetBooks();

        var existing = books.FirstOrDefault(b =>
            b.Title == book.Title &&
            b.Author == book.Author);

        if (existing == null)
            return;

        existing.LastOpened = DateTime.Now;

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