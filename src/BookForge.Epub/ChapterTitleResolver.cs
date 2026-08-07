using System.Text.RegularExpressions;

namespace BookForge.Epub;

public class ChapterTitleResolver
{
    public string Resolve(string html, string fallbackTitle)
    {
        // 1. H1-H3 címek
        var heading = Regex.Match(
            html,
            @"<h[1-3][^>]*>(.*?)</h[1-3]>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (heading.Success)
        {
            var title = CleanText(heading.Groups[1].Value);

            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }

        // 2. HTML <title>
        var pageTitle = Regex.Match(
            html,
            @"<title[^>]*>(.*?)</title>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (pageTitle.Success)
        {
            var title = CleanText(pageTitle.Groups[1].Value);

            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }

        // 3. Első <p>
        var paragraph = Regex.Match(
            html,
            @"<p[^>]*>(.*?)</p>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (paragraph.Success)
        {
            var title = CleanText(paragraph.Groups[1].Value);

            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }

        return fallbackTitle;
    }

    private string CleanText(string text)
    {
        return Regex.Replace(
            text,
            "<.*?>",
            string.Empty).Trim();
    }
}