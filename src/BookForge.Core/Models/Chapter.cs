using System;

namespace BookForge.Core.Models;

public class Chapter
{
    public string Title { get; set; } = string.Empty;

    // Fejezet sorrendje a könyvben
    public int Order { get; set; }

    // Meghatározza, hogy ez a rész beleszámít-e
    // a fejezetszámba és az olvasási haladásba.
    public bool CountsAsChapter { get; set; } = true;

    // EPUB fájl adatok
    public string FilePath { get; set; } = string.Empty;

    public string Href { get; set; } = string.Empty;

    // Tartalom - tisztított szöveg
    public string Content { get; set; } = string.Empty;

    // Eredeti EPUB/XHTML tartalom
    public string HtmlContent { get; set; } = string.Empty;

    // Segéd adatok
    public int WordCount { get; set; }

    public bool IsRead { get; set; }

    // Dátumok
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime? LastOpened { get; set; }

    public override string ToString()
    {
        return Title;
    }
}
