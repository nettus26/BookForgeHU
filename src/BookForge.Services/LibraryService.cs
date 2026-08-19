using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using BookForge.Core.Models;

namespace BookForge.Services;

public class LibraryService
{
    private readonly string libraryFile =
        "bookforge-library.json";

    private readonly string tempLibraryFile =
        "bookforge-library.json.tmp";

    private readonly string backupLibraryFile =
        "bookforge-library.json.bak";


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
                    libraryFile,
                    Encoding.UTF8);

            return
                JsonSerializer.Deserialize<List<Book>>(
                    json)
                ?? new List<Book>();
        }
        catch
        {
            // Ha a fő fájl sérült, megpróbáljuk a biztonsági mentést.
            if (File.Exists(backupLibraryFile))
            {
                try
                {
                    var backupJson =
                        File.ReadAllText(
                            backupLibraryFile,
                            Encoding.UTF8);

                    return
                        JsonSerializer.Deserialize<List<Book>>(
                            backupJson)
                        ?? new List<Book>();
                }
                catch
                {
                    // A mentés sem olvasható.
                }
            }

            return new List<Book>();
        }
    }


    // =========================================================
    // KÖNYV HOZZÁADÁSA
    // =========================================================

    public void AddBook(Book book)
    {
        if (book == null)
        {
            return;
        }

        var books =
            GetBooks();

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
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

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
        if (book == null)
        {
            return;
        }

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
        if (book == null)
        {
            return;
        }

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
        if (book == null)
        {
            return;
        }

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
        if (book == null)
        {
            return null;
        }

        var books =
            GetBooks();

        return books.FirstOrDefault(
            b =>
                b.Title == book.Title &&
                b.Author == book.Author);
    }


    // =========================================================
    // OLVASÓ BEÁLLÍTÁSOK MENTÉSE
    // =========================================================

    public void UpdateReaderSettings(
        Book book,
        double fontSize,
        string fontFamily,
        double lineSpacing,
        bool darkMode)
    {
        if (book == null)
        {
            return;
        }

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

        existing.ReaderFontSize =
            fontSize;

        existing.ReaderFontFamily =
            string.IsNullOrWhiteSpace(fontFamily)
                ? "Georgia"
                : fontFamily;

        existing.ReaderLineSpacing =
            lineSpacing > 0
                ? lineSpacing
                : 1.5;

        existing.ReaderDarkMode =
            darkMode;

        existing.LastOpened =
            DateTime.Now;

        Save(books);
    }


    // =========================================================
    // FEJEZET OLVASOTTKÉNT JELÖLÉSE
    // =========================================================

    public void MarkChapterAsRead(
        Book book,
        string chapterPath)
    {
        if (book == null ||
            string.IsNullOrWhiteSpace(
                chapterPath))
        {
            return;
        }

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

        var normalizedPath =
            NormalizePath(
                chapterPath);

        var chapter =
            existing.Chapters.FirstOrDefault(
                c =>
                    string.Equals(
                        NormalizePath(c.FilePath),
                        normalizedPath,
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        NormalizePath(c.Href),
                        normalizedPath,
                        StringComparison.OrdinalIgnoreCase));

        if (chapter == null)
        {
            return;
        }

        chapter.IsRead =
            true;

        chapter.LastOpened =
            DateTime.Now;

        Save(books);
    }


    // =========================================================
    // SEGÉD: EPUB ÚTVONAL NORMALIZÁLÁSA
    // =========================================================

    private static string NormalizePath(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(
            path))
        {
            return string.Empty;
        }

        return path
            .Replace("\\", "/")
            .Trim()
            .TrimStart('/');
    }


    // =========================================================
    // BIZTONSÁGOS MENTÉS
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

        // Először ideiglenes fájlba írunk.
        File.WriteAllText(
            tempLibraryFile,
            json,
            new UTF8Encoding(false));

        // A korábbi mentésből biztonsági másolat készül.
        if (File.Exists(libraryFile))
        {
            try
            {
                File.Copy(
                    libraryFile,
                    backupLibraryFile,
                    true);
            }
            catch
            {
                // A biztonsági másolat hibája önmagában
                // ne akadályozza meg a normál mentést.
            }
        }

        // Az ideiglenes fájlt csak sikeres teljes írás után
        // tesszük a tényleges könyvtári fájl helyére.
        try
        {
            File.Move(
                tempLibraryFile,
                libraryFile,
                true);
        }
        catch
        {
            // Ha a csere nem sikerült, a korábbi fájl
            // megmarad, és a hibát továbbadjuk.
            throw;
        }
    }
}
