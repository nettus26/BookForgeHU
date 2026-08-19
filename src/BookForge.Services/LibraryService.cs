using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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

            var books =
                JsonSerializer.Deserialize<List<Book>>(
                    json)
                ?? new List<Book>();

            // A korábban mentett könyvek még nem tartalmazták
            // a CountsAsChapter mezőt. Ezeknél a bool alapértéke
            // false lenne, ezért betöltéskor visszaállítjuk a
            // valódi fejezetek jelölését a cím alapján.
            MigrateChapterCounting(books);

            return books;
        }
        catch
        {
            return new List<Book>();
        }
    }


    // =========================================================
    // KÖNYVEK MENTÉSÉNEK KOMPATIBILITÁSI MIGRÁCIÓJA
    // =========================================================

    private void MigrateChapterCounting(
        List<Book> books)
    {
        var changed = false;

        foreach (var book in books)
        {
            if (book.Chapters == null ||
                book.Chapters.Count == 0)
            {
                continue;
            }

            // Csak a régi, már mentett könyveknél szükséges.
            // Ha van legalább egy már helyesen jelölt fejezet,
            // nem írjuk felül az új EPUB-reader eredményét.
            if (book.Chapters.Any(
                c => c.CountsAsChapter))
            {
                continue;
            }

            foreach (var chapter in book.Chapters)
            {
                var counted =
                    IsCountedChapterTitle(
                        chapter.Title);

                if (chapter.CountsAsChapter != counted)
                {
                    chapter.CountsAsChapter =
                        counted;

                    changed = true;
                }
            }
        }

        if (changed)
        {
            Save(books);
        }
    }


    // =========================================================
    // FEJEZET CÍME ALAPJÁN SZÁMÍTANDÓ-E
    // =========================================================

    private static bool IsCountedChapterTitle(
        string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalized =
            title.Trim();

        // Számmal kezdődő cím:
        // "1. fejezet", "3 ELI", "12. rész", stb.
        if (Regex.IsMatch(
            normalized,
            @"^\d+\b"))
        {
            return true;
        }

        // "Első fejezet", "TIZENKILENCEDIK FEJEZET", stb.
        if (Regex.IsMatch(
            normalized,
            @"(?i)\b(fejezet|rész)\b"))
        {
            return true;
        }

        // Az epilógusok számítsanak valódi olvasási egységnek.
        if (Regex.IsMatch(
            normalized,
            @"(?i)\bepilógus\b"))
        {
            return true;
        }

        // Egyéb részek, például "Nova", nem számítanak bele.
        return false;
    }


    // =========================================================
    // KÖNYV HOZZÁADÁSA
    // =========================================================

    public void AddBook(Book book)
    {
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
    // OLVASÓ BEÁLLÍTÁSOK MENTÉSE
    // =========================================================

    public void UpdateReaderSettings(
        Book book,
        double fontSize,
        string fontFamily,
        double lineSpacing,
        bool darkMode)
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
        if (string.IsNullOrWhiteSpace(
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
