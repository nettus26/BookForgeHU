using System;
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
        var cleanText =
            RemoveHtml(htmlContent);

        var chapterTitle =
            title;

        // Ha nincs értelmes TOC-cím,
        // megpróbáljuk az EPUB HTML-ből
        // kinyerni a fejezet címét.
        if (string.IsNullOrWhiteSpace(chapterTitle)
            ||
            chapterTitle.StartsWith(
                "Chapter ",
                StringComparison.OrdinalIgnoreCase))
        {
            chapterTitle =
                FindChapterTitle(
                    htmlContent,
                    chapterTitle);
        }

        return new Chapter
        {
            Title = chapterTitle,

            Order = order,

            Content = cleanText,

            // FONTOS:
            // Nem építünk új HTML-t a fejezet köré.
            // Az EPUB eredeti HTML-je kerül
            // közvetlenül az olvasóba.
            HtmlContent = htmlContent,

            WordCount =
                CountWords(cleanText),

            IsRead = false,

            CreatedDate = DateTime.Now
        };
    }


    private string RemoveHtml(
        string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text =
            Regex.Replace(
                html,
                "<.*?>",
                string.Empty,
                RegexOptions.Singleline);

        text =
            System.Net.WebUtility
                .HtmlDecode(text);

        text =
            Regex.Replace(
                text,
                @"\s+",
                " ");

        return text.Trim();
    }


    private int CountWords(
        string text)
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

        var match =
            Regex.Match(
                html,
                @"<h[1-3][^>]*>(.*?)</h[1-3]>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        if (match.Success)
        {
            var extracted =
                Regex.Replace(
                    match.Groups[1].Value,
                    "<.*?>",
                    string.Empty,
                    RegexOptions.Singleline);

            extracted =
                System.Net.WebUtility
                    .HtmlDecode(extracted)
                    .Trim();

            if (!string.IsNullOrWhiteSpace(extracted))
                return extracted;
        }

        return fallbackTitle;
    }
}