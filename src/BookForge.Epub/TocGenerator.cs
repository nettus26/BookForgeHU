using BookForge.Core.Models;

namespace BookForge.Epub;

public class TocGenerator
{
    public Dictionary<string, string> Generate(Book book)
    {
        var toc = new Dictionary<string, string>();

        foreach (var chapter in book.Chapters)
        {
            toc[chapter.Href] = chapter.Title;
        }

        return toc;
    }
}