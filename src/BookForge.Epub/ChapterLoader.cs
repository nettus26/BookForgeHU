using System.Text.RegularExpressions;
using BookForge.Core.Models;

namespace BookForge.Epub;

public class ChapterLoader
{
    public Chapter Load(string title, string htmlContent, int order)
    {
        Console.WriteLine("BETÖLTÖTT HTML:");
        Console.WriteLine(htmlContent);

        var cleanText = RemoveHtml(htmlContent);

        return new Chapter
        {
            Title = title,
            Order = order,
            Content = cleanText
        };
    }

    private string RemoveHtml(string html)
    {
        return Regex.Replace(html, "<.*?>", string.Empty)
                    .Trim();
    }
}