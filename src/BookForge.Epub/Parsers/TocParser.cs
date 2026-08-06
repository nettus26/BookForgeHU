using System.Text.RegularExpressions;

namespace BookForge.Epub.Parsers;

public class TocParser
{
    public Dictionary<string, string> Parse(string xhtml)
    {
        var result = new Dictionary<string, string>();

        var matches = Regex.Matches(
            xhtml,
            @"href=""([^""]+)"".*?>(.*?)</a>",
            RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var href = match.Groups[1].Value;
            // Anchor (#) eltávolítása
            var index = href.IndexOf('#');

            if (index >= 0)
            {
                href = href[..index];
            }

            // Perjelek egységesítése
            href = href.Replace("\\", "/");
            href = href.TrimStart('/');
            var title = Regex.Replace(
         match.Groups[2].Value,
         "<.*?>",
         string.Empty).Trim();

            if (!string.IsNullOrEmpty(href) &&
                !string.IsNullOrEmpty(title))
            {
                result[href] = title;
            }
        }
        foreach (var item in result)
        {
            System.Diagnostics.Debug.WriteLine(
                $"{item.Key} -> {item.Value}");
        }
        return result;
    }
}