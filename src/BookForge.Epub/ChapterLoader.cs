using System;
using System.Text.RegularExpressions;
using BookForge.Core.Models;

namespace BookForge.Epub;

public class ChapterLoader
{
    public Chapter Load(string title, string htmlContent, int order)
    {
        var cleanText = RemoveHtml(htmlContent);

        return new Chapter
        {
            Title = title.StartsWith("Chapter ")
                ? FindChapterTitle(htmlContent, title)
                : title,

            Order = order,

            Content = cleanText,

            WordCount = CountWords(cleanText),

            IsRead = false,

            CreatedDate = DateTime.Now
        };
    }

    private string RemoveHtml(string html)
    {
        var text = Regex.Replace(
            html,
            "<.*?>",
            string.Empty);

        text = Regex.Replace(
            text,
            @"\s+",
            " ");

        return text.Trim();
    }

    private int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }

    private string FindChapterTitle(string html, string fallbackTitle)
    {
        var match = Regex.Match(
            html,
            @"<h[1-3][^>]*>(.*?)</h[1-3]>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (match.Success)
        {
            var title = Regex.Replace(
                match.Groups[1].Value,
                "<.*?>",
                string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }

        return fallbackTitle;
    }
}