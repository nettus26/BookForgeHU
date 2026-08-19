using System;
using System.IO;
using System.Linq;
using BookForge.Core.Models;
using BookForge.Epub;
using BookForge.Epub.Interfaces;

namespace BookForge.Services;

public class ImportService
{
    private readonly IEpubReader reader;
    private readonly LibraryService library;


    // =========================================================
    // KONSTRUKTOR
    // =========================================================

    public ImportService()
    {
        reader =
            new EpubReaderV2();

        library =
            new LibraryService();
    }


    // =========================================================
    // EPUB IMPORTÁLÁSA
    // =========================================================

    public Book ImportEpub(
        string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "Az EPUB fájl elérési útja üres.",
                nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "Az EPUB fájl nem található.",
                filePath);
        }

        if (!string.Equals(
                Path.GetExtension(filePath),
                ".epub",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "A kiválasztott fájl nem EPUB formátumú.");
        }


        // =====================================================
        // EPUB BEOLVASÁSA
        // =====================================================

        Book book;

        try
        {
            book =
                reader.Load(filePath);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(
                "Az EPUB könyv nem tölthető be. " +
                "A fájl sérült lehet, vagy nem megfelelő EPUB-szerkezetű.",
                ex);
        }

        if (book == null)
        {
            throw new InvalidDataException(
                "Az EPUB könyv betöltése üres eredményt adott.");
        }


        // =====================================================
        // ALAPADATOK ELLENŐRZÉSE
        // =====================================================

        if (string.IsNullOrWhiteSpace(book.Title))
        {
            book.Title =
                Path.GetFileNameWithoutExtension(filePath);
        }

        if (string.IsNullOrWhiteSpace(book.Author))
        {
            book.Author =
                "Ismeretlen szerző";
        }

        if (book.Chapters == null)
        {
            throw new InvalidDataException(
                "Az EPUB nem tartalmaz feldolgozható fejezetlistát.");
        }

        if (book.Chapters.Count == 0)
        {
            throw new InvalidDataException(
                "Az EPUB nem tartalmaz olvasható fejezetet.");
        }


        book.FilePath =
            Path.GetFullPath(filePath);


        // =====================================================
        // DUPLIKÁLT KÖNYV ELLENŐRZÉSE
        // =====================================================

        var existingBooks =
            library.GetBooks();

        var existing =
            existingBooks.FirstOrDefault(
                b =>
                    string.Equals(
                        b.Title,
                        book.Title,
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    string.Equals(
                        b.Author,
                        book.Author,
                        StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            return existing;
        }


        // =====================================================
        // KÖNYV MENTÉSE A LIBRARY-BE
        // =====================================================

        library.AddBook(book);

        return book;
    }
}
