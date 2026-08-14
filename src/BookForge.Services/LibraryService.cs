using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BookForge.Core.Models;

namespace BookForge.Services;

public class LibraryService
{
    private readonly string libraryFile =
        "bookforge-library.json";


    // =========================================================
    // KÖNYVEK BETÖLTÉSE
    // =========================================================

    public List<Book> GetBooks()
    {
        if (!File.Exists(libraryFile))
        {
            return new List<Book>();
        }

        try
        {
            var json =
                File.ReadAllText(
                    libraryFile);

            return
                JsonSerializer.Deserialize<List<Book>>(
                    json)
                ?? new List<Book>();
        }
        catch
        {
            return new List<Book>();
        }
    }


    // =========================================================
    // KÖNYV HOZZÁADÁSA
    // =========================================================

    public void AddBook(Book book)
    {
        var books =
            GetBooks();

        // Duplikáció ellenőrzés
        var exists =
            books.Any(b =>
                b.Title == book.Title &&
                b.Author == book.Author);

        if (exists)
        {
            return;
        }

        books.Add(book);

        Save(books);
    }


    // =========================================================
    // KÖNYV KERESÉSE
    // =========================================================

    public Book? FindBook(
        string title)
    {
        var books =
            GetBooks();

        return books.FirstOrDefault(
            b =>
                b.Title.Contains(
                    title,
                    StringComparison.OrdinalIgnoreCase));
    }


    // =========================================================
    // KÖNYV TÖRLÉSE
    // =========================================================

    public void RemoveBook(
        Book book)
    {
        var books =
            GetBooks();

        var existing =
            books.FirstOrDefault(
                b =>
                    b.Title == book.Title &&
                    b.Author == book.Author);

        if (existing == null)
        {
            return;
        }

        books.Remove(existing);

        Save(books);
    }


    // =========================================================
    // UTOLSÓ MEGNYITÁS MENTÉSE
    // =========================================================

    public void UpdateLastOpened(
        Book book)
    {
        var books =
            GetBooks();

        var existing =
            books.FirstOrDefault(
                b =>
                    b.Title == book.Title &&
                    b.Author == book.Author);

        if (existing == null)
        {
            return;
        }

        existing.LastOpened =
            DateTime.Now;

        Save(books);
    }


    // =========================================================
    // OLVASÁSI POZÍCIÓ MENTÉSE
    // =========================================================

    public void UpdateReadingPosition(
        Book book,
        string chapterPath,
        double scrollPosition)
    {
        var books =
            GetBooks();

        var existing =
            books.FirstOrDefault(
                b =>
                    b.Title == book.Title &&
                    b.Author == book.Author);

        if (existing == null)
        {
            return;
        }

        existing.LastOpened =
            DateTime.Now;

        existing.LastChapterPath =
            chapterPath ?? string.Empty;

        existing.LastScrollPosition =
            scrollPosition;

        Save(books);
    }


    // =========================================================
    // OLVASÁSI POZÍCIÓ BETÖLTÉSE
    // =========================================================

    public Book? GetSavedReadingPosition(
        Book book)
    {
        var books =
            GetBooks();

        return books.FirstOrDefault(
            b =>
                b.Title == book.Title &&
                b.Author == book.Author);
    }


    // =========================================================
    // MENTÉS
    // =========================================================

    private void Save(
        List<Book> books)
    {
        var json =
            JsonSerializer.Serialize(
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