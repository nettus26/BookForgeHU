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
            var title = match.Groups[2].Value.Trim();

            if (!string.IsNullOrEmpty(href) &&
                !string.IsNullOrEmpty(title))
            {
                result[href] = title;
            }
        }

        return result;
    }
}