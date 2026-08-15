using System;
using System.Collections.Generic;

namespace BookForge.Core.Models;

public class Book
{
    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;


    // =========================================================
    // KÖNYV ADATOK
    // =========================================================

    public string Language { get; set; } = "hu";

    public string FilePath { get; set; } = string.Empty;

    public string CoverImage { get; set; } = string.Empty;

    public string ISBN { get; set; } = string.Empty;


    // =========================================================
    // RENDSZER ADATOK
    // =========================================================

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime? LastOpened { get; set; }


    // =========================================================
    // OLVASÁSI POZÍCIÓ
    // =========================================================

    // Az utoljára olvasott fejezet elérési útja
    public string LastChapterPath { get; set; } = string.Empty;

    // Az utolsó mentett görgetési pozíció pixelben
    public double LastScrollPosition { get; set; }


    // =========================================================
    // OLVASÓ BEÁLLÍTÁSOK
    // =========================================================

    // A könyvhöz tartozó betűméret
    public double ReaderFontSize { get; set; } = 20;

    // A könyvhöz tartozó betűtípus
    public string ReaderFontFamily { get; set; } = "Georgia";

    // A könyvhöz tartozó sorköz
    public double ReaderLineSpacing { get; set; } = 1.5;

    // A könyvhöz tartozó világos / sötét mód
    public bool ReaderDarkMode { get; set; } = false;


    // =========================================================
    // FEJEZETEK
    // =========================================================

    public List<Chapter> Chapters { get; set; } = new();


    // =========================================================
    // TARTALOMJEGYZÉK
    // =========================================================

    public Dictionary<string, string> TableOfContents { get; set; } = new();
}
