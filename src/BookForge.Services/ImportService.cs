using System;
using System.Collections.Generic;
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
        if (string.IsNullOrWhiteSpace(
            filePath))
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

        var book =
            reader.Load(filePath);

        if (book == null)
        {
            throw new InvalidDataException(
                "Az EPUB könyv nem tölthető be.");
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
            // A már meglévő könyv objektumát adjuk vissza,
            // így az olvasási állapot, kedvenc és beállítások
            // nem vesznek el újraimportáláskor.
            return existing;
        }


        // =====================================================
        // KÖNYV MENTÉSE A LIBRARY-BE
        // =====================================================

        library.AddBook(
            book);

        return book;
    }
}
