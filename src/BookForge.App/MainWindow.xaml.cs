using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using BookForge.Core.Models;
using BookForge.Services;
using BookForge.Epub;
using BookForge.App.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace BookForge.App;

public partial class MainWindow : Window
{
    private readonly ImportService importer;
    private readonly LibraryService library;
    private readonly CoverService coverService;

    private readonly List<Book> books = new();

    private readonly WebView2 contentViewer;

    private string? pendingFragment;


    public MainWindow()
    {
        InitializeComponent();

        contentViewer = new WebView2();

        ReaderHost.Children.Add(
            contentViewer);

        importer =
            new ImportService();

        library =
            new LibraryService();

        coverService =
            new CoverService();

        contentViewer.NavigationStarting +=
            ContentViewer_NavigationStarting;

        contentViewer.NavigationCompleted +=
            ContentViewer_NavigationCompleted;

        LoadLibrary();

        Loaded += MainWindow_Loaded;
    }


    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            await contentViewer.EnsureCoreWebView2Async();

            contentViewer.NavigateToString(
                CreateReaderHtml(
                    "<p>Válassz ki egy fejezetet.</p>"));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "WebView2 hiba");
        }
    }


    private string CreateReaderHtml(
        string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            html =
                "<p>Válassz ki egy fejezetet.</p>";
        }

        var style = """
            <style>
                html {
                    overflow-x: hidden;
                }

                body {
                    font-family: Georgia, serif;
                    font-size: 20px;
                    line-height: 1.7;
                    margin: 30px;
                    color: #222;
                    background: white;
                    overflow-x: hidden;
                    word-wrap: break-word;
                    overflow-wrap: break-word;
                }

                h1 {
                    font-size: 32px;
                    margin-top: 0;
                    margin-bottom: 20px;
                }

                h2 {
                    font-size: 26px;
                    margin-top: 24px;
                    margin-bottom: 16px;
                }

                h3 {
                    font-size: 23px;
                    margin-top: 20px;
                    margin-bottom: 14px;
                }

                p {
                    margin-top: 0;
                    margin-bottom: 16px;
                }

                img {
                    display: block;
                    max-width: 100% !important;
                    width: auto !important;
                    height: auto !important;
                    box-sizing: border-box;
                    margin-left: auto;
                    margin-right: auto;
                }

                table {
                    max-width: 100%;
                    width: auto;
                    box-sizing: border-box;
                }

                pre,
                code {
                    max-width: 100%;
                    white-space: pre-wrap;
                    overflow-wrap: break-word;
                }

                blockquote {
                    margin-left: 20px;
                    margin-right: 20px;
                }

                a {
                    cursor: pointer;
                }
            </style>
            """;

        if (html.Contains(
                "</head>",
                StringComparison.OrdinalIgnoreCase))
        {
            return
                html.Replace(
                    "</head>",
                    style + "</head>",
                    StringComparison.OrdinalIgnoreCase);
        }

        return $"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                {style}
            </head>
            <body>
                {html}
            </body>
            </html>
            """;
    }


    private void LoadLibrary()
    {
        var savedBooks =
            library.GetBooks();

        foreach (var savedBook in savedBooks)
        {
            try
            {
                if (File.Exists(
                        savedBook.FilePath))
                {
                    var reader =
                        new EpubReader();

                    var fullBook =
                        reader.Load(
                            savedBook.FilePath);

                    books.Add(
                        fullBook);

                    BookList.Items.Add(
                        fullBook);
                }
                else
                {
                    books.Add(
                        savedBook);

                    BookList.Items.Add(
                        savedBook);
                }
            }
            catch
            {
                books.Add(
                    savedBook);

                BookList.Items.Add(
                    savedBook);
            }
        }
    }


    private void AddEpub_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog =
            new OpenFileDialog
            {
                Filter =
                    "EPUB könyv (*.epub)|*.epub"
            };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var book =
                    importer.ImportEpub(
                        dialog.FileName);

                books.Add(
                    book);

                BookList.Items.Add(
                    book);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "EPUB betöltési hiba");
            }
        }
    }


    private void DeleteBook_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (BookList.SelectedItem is Book book)
        {
            var result =
                MessageBox.Show(
                    $"Biztosan törlöd ezt a könyvet?\n\n{book.Title}",
                    "Könyv törlése",
                    MessageBoxButton.YesNo);

            if (result ==
                MessageBoxResult.Yes)
            {
                library.RemoveBook(
                    book);

                books.Remove(
                    book);

                BookList.Items.Remove(
                    book);

                ChapterList.Items.Clear();

                CoverImageBox.Source =
                    null;

                contentViewer.NavigateToString(
                    CreateReaderHtml(
                        "<p>Válassz ki egy fejezetet.</p>"));
            }
        }
    }


    private void BookList_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (BookList.SelectedItem is Book book)
        {
            BookTitleText.Text =
                book.Title;

            BookAuthorText.Text =
                book.Author;

            BookLanguageText.Text =
                book.Language;

            BookDateText.Text =
                book.CreatedDate.ToString(
                    "yyyy.MM.dd.");

            LoadCover(
                book);

            ChapterList.Items.Clear();

            foreach (var chapter in book.Chapters)
            {
                ChapterList.Items.Add(
                    chapter);
            }
        }
    }


    private void LoadCover(
        Book book)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(
                    book.CoverImage)
                &&
                File.Exists(
                    book.CoverImage))
            {
                var image =
                    new BitmapImage();

                image.BeginInit();

                image.UriSource =
                    new Uri(
                        book.CoverImage);

                image.CacheOption =
                    BitmapCacheOption.OnLoad;

                image.EndInit();

                CoverImageBox.Source =
                    image;

                return;
            }

            CoverImageBox.Source =
                coverService.CreateDefaultCover(
                    book.Title,
                    book.Author);
        }
        catch
        {
            CoverImageBox.Source =
                coverService.CreateDefaultCover(
                    book.Title,
                    book.Author);
        }
    }


    private void ChapterList_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ChapterList.SelectedItem is Chapter chapter)
        {
            ShowChapter(
                chapter);
        }
    }


    private void ShowChapter(
        Chapter chapter)
    {
        if (string.IsNullOrWhiteSpace(
                chapter.HtmlContent))
        {
            return;
        }

        try
        {
            var readerHtml =
                CreateReaderHtml(
                    chapter.HtmlContent);

            contentViewer.NavigateToString(
                readerHtml);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Fejezet megjelenítési hiba");
        }
    }


    // =========================================================
    // BELSŐ EPUB LINK ELKAPÁSA
    // =========================================================

    private void ContentViewer_NavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(
                e.Uri))
        {
            return;
        }

        const string prefix =
            "bookforge://chapter/";

        if (!e.Uri.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;

        try
        {
            var target =
                e.Uri[prefix.Length..];

            var fragment =
                string.Empty;

            var fragmentIndex =
                target.IndexOf('#');

            if (fragmentIndex >= 0)
            {
                fragment =
                    target[(fragmentIndex + 1)..];

                target =
                    target[..fragmentIndex];
            }

            var chapterPath =
                Uri.UnescapeDataString(
                    target);

            NavigateToInternalChapter(
                chapterPath,
                fragment);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Belső EPUB link hiba");
        }
    }


    // =========================================================
    // BELSŐ FEJEZET MEGKERESÉSE
    // =========================================================

    private void NavigateToInternalChapter(
        string chapterPath,
        string fragment)
    {
        if (BookList.SelectedItem
            is not Book book)
        {
            return;
        }

        var normalizedTarget =
            NormalizeChapterPath(
                chapterPath);

        var chapter =
            book.Chapters.FirstOrDefault(
                c =>
                    string.Equals(
                        NormalizeChapterPath(
                            c.FilePath),
                        normalizedTarget,
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        NormalizeChapterPath(
                            c.Href),
                        normalizedTarget,
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        Path.GetFileName(
                            c.FilePath),
                        Path.GetFileName(
                            normalizedTarget),
                        StringComparison.OrdinalIgnoreCase));

        if (chapter == null)
        {
            return;
        }

        pendingFragment =
            fragment;

        ChapterList.SelectedItem =
            chapter;

        ChapterList.ScrollIntoView(
            chapter);

        ShowChapter(
            chapter);
    }


    // =========================================================
    // FEJEZET BETÖLTÉSE UTÁNI UGRÁS
    // =========================================================

    private async void ContentViewer_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
            return;

        if (string.IsNullOrWhiteSpace(
                pendingFragment))
        {
            return;
        }

        var fragment =
            pendingFragment;

        pendingFragment =
            null;

        try
        {
            var escaped =
                EscapeJavaScriptString(
                    fragment);

            var script =
                "(function() {" +
                "const element = " +
                "document.getElementById('" +
                escaped +
                "') || " +
                "document.getElementsByName('" +
                escaped +
                "')[0];" +
                "if (element) {" +
                "element.scrollIntoView({" +
                "behavior: 'smooth'," +
                "block: 'start'" +
                "});" +
                "}" +
                "})();";

            await contentViewer.ExecuteScriptAsync(
                script);
        }
        catch
        {
            // Ha a célpont nem található,
            // nem állítjuk le az olvasót.
        }
    }


    // =========================================================
    // JAVASCRIPT STRING ESCAPELÉSE
    // =========================================================

    private static string EscapeJavaScriptString(
        string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }


    // =========================================================
    // EPUB ÚTVONAL NORMALIZÁLÁSA
    // =========================================================

    private static string NormalizeChapterPath(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            return string.Empty;
        }

        path =
            path
                .Replace("\\", "/")
                .Trim();

        var fragmentIndex =
            path.IndexOf('#');

        if (fragmentIndex >= 0)
        {
            path =
                path[..fragmentIndex];
        }

        path =
            Uri.UnescapeDataString(
                path);

        var parts =
            path.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

        var result =
            new List<string>();

        foreach (var part in parts)
        {
            if (part == ".")
                continue;

            if (part == "..")
            {
                if (result.Count > 0)
                    result.RemoveAt(
                        result.Count - 1);

                continue;
            }

            result.Add(
                part);
        }

        return string.Join(
            "/",
            result);
    }
}