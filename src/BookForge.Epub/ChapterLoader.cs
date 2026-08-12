using System;
using System.Net;
using System.Text.RegularExpressions;
using BookForge.Core.Models;

namespace BookForge.Epub;

public class ChapterLoader
{
    public Chapter Load(
        string title,
        string htmlContent,
        int order)
    {
        var cleanText = RemoveHtml(htmlContent);

        return new Chapter
        {
            Title = title.StartsWith(
                "Chapter ",
                StringComparison.OrdinalIgnoreCase)
                ? FindChapterTitle(htmlContent, title)
                : title,

            Order = order,

            Content = cleanText,

            // AZ EREDETI EPUB HTML-TARTALOM
            HtmlContent = htmlContent,

            WordCount = CountWords(cleanText),

            IsRead = false,

            CreatedDate = DateTime.Now
        };
    }

    private string RemoveHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = Regex.Replace(
            html,
            "<.*?>",
            string.Empty,
            RegexOptions.Singleline);

        text = WebUtility.HtmlDecode(text);

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

    private string FindChapterTitle(
        string html,
        string fallbackTitle)
    {
        if (string.IsNullOrWhiteSpace(html))
            return fallbackTitle;

        var match = Regex.Match(
            html,
            @"<h[1-3][^>]*>(.*?)</h[1-3]>",
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline);

        if (match.Success)
        {
            var chapterTitle =
                Regex.Replace(
                    match.Groups[1].Value,
                    "<.*?>",
                    string.Empty,
                    RegexOptions.Singleline);

            chapterTitle =
                WebUtility.HtmlDecode(
                    chapterTitle).Trim();

            if (!string.IsNullOrWhiteSpace(
                chapterTitle))
            {
                return chapterTitle;
            }
        }

        return fallbackTitle;
    }
}