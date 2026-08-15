using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Text.RegularExpressions;
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


    // =========================================================
    // OLVASÓ BEÁLLÍTÁSOK
    // =========================================================

    private double readerFontSize = 20;

    private string readerFontFamily = "Georgia";

    private double readerLineSpacing = 1.7;

    private bool darkMode = false;


    // =========================================================
    // OLVASÁSI POZÍCIÓ
    // =========================================================

    private readonly DispatcherTimer readingPositionTimer;

    private Book? currentBook;

    private Chapter? currentChapter;

    private bool restoringReadingPosition = false;


    // =========================================================
    // KONSTRUKTOR
    // =========================================================

    public MainWindow()
    {
        InitializeComponent();

        UpdateFontSizeDisplay();

        contentViewer =
            new WebView2();

        ReaderHost.Children.Add(
            contentViewer);

        importer =
            new ImportService();

        library =
            new LibraryService();

        coverService =
            new CoverService();


        readingPositionTimer =
            new DispatcherTimer();

        readingPositionTimer.Interval =
            TimeSpan.FromSeconds(1);

        readingPositionTimer.Tick +=
            ReadingPositionTimer_Tick;


        contentViewer.NavigationStarting +=
            ContentViewer_NavigationStarting;

        contentViewer.NavigationCompleted +=
            ContentViewer_NavigationCompleted;


        LoadLibrary();

        Loaded +=
            MainWindow_Loaded;

        Closing +=
            MainWindow_Closing;
    }


    // =========================================================
    // WEBVIEW2 INDÍTÁSA
    // =========================================================

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


    // =========================================================
    // OLVASÓ HTML
    // =========================================================

    private string CreateReaderHtml(
        string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            html =
                "<p>Válassz ki egy fejezetet.</p>";
        }

        var background =
            darkMode
                ? "#1e1e1e"
                : "white";

        var textColor =
            darkMode
                ? "#eeeeee"
                : "#222222";

        var headingColor =
            darkMode
                ? "#ffffff"
                : "#222222";

        var linkColor =
            darkMode
                ? "#8ab4f8"
                : "#0645ad";

        var fontSize =
            readerFontSize.ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        var lineSpacing =
            readerLineSpacing.ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        var h1Size =
            (readerFontSize * 1.6).ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        var h2Size =
            (readerFontSize * 1.3).ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        var h3Size =
            (readerFontSize * 1.15).ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        var style =
            "<style>" +

            "html {" +
            "overflow-x: hidden;" +
            "background: " + background + ";" +
            "}" +

            "body {" +
            "font-family: '" + readerFontFamily + "', serif !important;" +
            "font-size: " + fontSize + "px !important;" +
            "line-height: " + lineSpacing + " !important;" +
            "margin: 30px;" +
            "color: " + textColor + ";" +
            "background: " + background + ";" +
            "overflow-x: hidden;" +
            "word-wrap: break-word;" +
            "overflow-wrap: break-word;" +
            "}" +

            "h1 {" +
            "font-family: '" + readerFontFamily + "', serif !important;" +
            "font-size: " + h1Size + "px !important;" +
            "margin-top: 0;" +
            "margin-bottom: 20px;" +
            "color: " + headingColor + ";" +
            "}" +

            "h2 {" +
            "font-family: '" + readerFontFamily + "', serif !important;" +
            "font-size: " + h2Size + "px !important;" +
            "margin-top: 24px;" +
            "margin-bottom: 16px;" +
            "color: " + headingColor + ";" +
            "}" +

            "h3 {" +
            "font-family: '" + readerFontFamily + "', serif !important;" +
            "font-size: " + h3Size + "px !important;" +
            "margin-top: 20px;" +
            "margin-bottom: 14px;" +
            "color: " + headingColor + ";" +
            "}" +

            "p {" +
            "font-family: '" + readerFontFamily + "', serif !important;" +
            "font-size: " + fontSize + "px !important;" +
            "line-height: " + lineSpacing + " !important;" +
            "margin-top: 0;" +
            "margin-bottom: 16px;" +
            "}" +

            "a {" +
            "color: " + linkColor + ";" +
            "cursor: pointer;" +
            "}" +

            "img {" +
            "display: block;" +
            "max-width: 100% !important;" +
            "width: auto !important;" +
            "height: auto !important;" +
            "box-sizing: border-box;" +
            "margin-left: auto;" +
            "margin-right: auto;" +
            "}" +

            "table {" +
            "max-width: 100%;" +
            "width: auto;" +
            "box-sizing: border-box;" +
            "}" +

            "pre, code {" +
            "max-width: 100%;" +
            "white-space: pre-wrap;" +
            "overflow-wrap: break-word;" +
            "}" +

            "blockquote {" +
            "margin-left: 20px;" +
            "margin-right: 20px;" +
            "}" +

            "</style>";

        if (html.Contains(
            "</head>",
            StringComparison.OrdinalIgnoreCase))
        {
            return html.Replace(
                "</head>",
                style + "</head>",
                StringComparison.OrdinalIgnoreCase);
        }

        return
            "<!DOCTYPE html>" +
            "<html>" +
            "<head>" +
            "<meta charset=\"utf-8\">" +
            style +
            "</head>" +
            "<body>" +
            html +
            "</body>" +
            "</html>";
    }


    // =========================================================
    // KÖNYVTÁR BETÖLTÉSE
    // =========================================================

    private void LoadLibrary()
    {
        var savedBooks =
            library.GetBooks();

        foreach (var savedBook in savedBooks)
        {
            try
            {
                Book bookToLoad;

                if (File.Exists(
                    savedBook.FilePath))
                {
                    var reader =
                        new EpubReaderV2();

                    var fullBook =
                        reader.Load(
                            savedBook.FilePath);

                    fullBook.LastOpened =
                        savedBook.LastOpened;

                    fullBook.LastChapterPath =
                        savedBook.LastChapterPath;

                    fullBook.LastScrollPosition =
                        savedBook.LastScrollPosition;

                    bookToLoad =
                        fullBook;
                }
                else
                {
                    bookToLoad =
                        savedBook;
                }

                books.Add(
                    bookToLoad);

                BookList.Items.Add(
                    bookToLoad);
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


    // =========================================================
    // EPUB HOZZÁADÁSA
    // =========================================================

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

                BookList.SelectedItem =
                    book;

                BookList.ScrollIntoView(
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


    // =========================================================
    // KÖNYV TÖRLÉSE
    // =========================================================

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
                SaveReadingPosition();

                library.RemoveBook(
                    book);

                books.Remove(
                    book);

                BookList.Items.Remove(
                    book);

                TocList.ItemsSource =
                    null;

                TocList.Items.Clear();

                ChapterList.ItemsSource =
                    null;

                ChapterList.Items.Clear();

                CoverImageBox.Source =
                    null;

                currentBook =
                    null;

                currentChapter =
                    null;

                contentViewer.NavigateToString(
                    CreateReaderHtml(
                        "<p>Válassz ki egy fejezetet.</p>"));
            }
        }
    }


    // =========================================================
    // KÖNYV KIVÁLASZTÁSA
    // =========================================================

    private void BookList_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (BookList.SelectedItem is not Book book)
        {
            return;
        }

        SaveReadingPosition();

        currentBook =
            book;

        currentChapter =
            null;

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


        // =====================================================
        // TARTALOMJEGYZÉK
        // =====================================================

        LoadTableOfContents(
            book);

        // =====================================================
        // FEJEZETLISTA
        // =====================================================

        LoadChapterList(
            book);

        RestoreLastReadingPosition(
            book);
    }


    // =========================================================
    // FEJEZETLISTA BETÖLTÉSE
    // =========================================================

    private void LoadChapterList(
        Book book)
    {
        ChapterList.ItemsSource =
            null;

        ChapterList.Items.Clear();

        if (book.Chapters == null ||
            book.Chapters.Count == 0)
        {
            return;
        }

        var chapters =
            book.Chapters
                .OrderBy(
                    c => c.Order)
                .ToList();

        ChapterList.ItemsSource =
            chapters;
    }


    // =========================================================
    // BORÍTÓ
    // =========================================================

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


    // =========================================================
    // TARTALOMJEGYZÉK KIVÁLASZTÁSA
    // =========================================================

    private void TocList_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TocList.SelectedItem is KeyValuePair<string, string> item)
        {
            NavigateToTocEntry(
                item.Key);
        }
    }


    // =========================================================
    // TARTALOMJEGYZÉK BETÖLTÉSE
    // =========================================================

    private void LoadTableOfContents(
        Book book)
    {
        TocList.ItemsSource =
            null;

        TocList.Items.Clear();

        if (book.Chapters == null ||
            book.Chapters.Count == 0)
        {
            return;
        }

        // A Tartalomjegyzék ugyanazokat a feldolgozott
        // fejezetcímeket használja, mint a Fejezetek lista.
        // Így nem az EPUB eredeti TOC-címeit (pl. Caly,
        // Mendax, Eli) jelenítjük meg.
        // Csak a ténylegesen számozott fejezetek kerüljenek
        // a Tartalomjegyzékbe. Az EPUB egyéb oldalai, például
        // "Hová mentél? [Hungarian]", így nem jelennek meg.
        var entries =
            book.Chapters
                .Where(
                    chapter =>
                        chapter != null &&
                        !string.IsNullOrWhiteSpace(
                            chapter.Title) &&
                        Regex.IsMatch(
                            chapter.Title.Trim(),
                            @"^\d{1,4}\b"))
                .OrderBy(
                    chapter => chapter.Order)
                .Select(
                    chapter =>
                    {
                        var path =
                            !string.IsNullOrWhiteSpace(
                                chapter.FilePath)
                                ? chapter.FilePath
                                : chapter.Href;

                        return new KeyValuePair<string, string>(
                            path ?? string.Empty,
                            chapter.Title.Trim());
                    })
                .Where(
                    item =>
                        !string.IsNullOrWhiteSpace(
                            item.Key))
                .ToList();

        TocList.ItemsSource =
            entries;
    }


    // =========================================================
    // TARTALOMJEGYZÉK ELEM MEGNYITÁSA
    // =========================================================

    private void NavigateToTocEntry(
        string tocPath)
    {
        if (currentBook == null)
        {
            return;
        }

        var normalizedTarget =
            NormalizeChapterPath(
                tocPath);

        var chapter =
            currentBook.Chapters.FirstOrDefault(
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
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        Path.GetFileName(
                            c.Href),
                        Path.GetFileName(
                            normalizedTarget),
                        StringComparison.OrdinalIgnoreCase));

        if (chapter == null)
        {
            return;
        }

        ChapterList.SelectedItem =
            chapter;

        ChapterList.ScrollIntoView(
            chapter);

        TocList.ScrollIntoView(
            TocList.SelectedItem);

        ShowChapter(
            chapter);
    }


    // =========================================================
    // FEJEZET KIVÁLASZTÁSA
    // =========================================================

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


    // =========================================================
    // FEJEZET MEGJELENÍTÉSE
    // =========================================================

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
            currentChapter =
                chapter;

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
    // BETŰMÉRET KIJELZŐ FRISSÍTÉSE
    // =========================================================

    private void UpdateFontSizeDisplay()
    {
        if (FontSizeText != null)
        {
            FontSizeText.Text =
                $"{readerFontSize:0} px";
        }
    }


    // =========================================================
    // A− BETŰMÉRET
    // =========================================================

    private void DecreaseFontButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SaveReadingPosition();

        readerFontSize -= 1;

        if (readerFontSize < 12)
        {
            readerFontSize = 12;
        }

        UpdateFontSizeDisplay();

        RefreshCurrentChapter();
    }


    // =========================================================
    // A+ BETŰMÉRET
    // =========================================================

    private void IncreaseFontButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SaveReadingPosition();

        readerFontSize += 1;

        if (readerFontSize > 40)
        {
            readerFontSize = 40;
        }

        UpdateFontSizeDisplay();

        RefreshCurrentChapter();
    }


    // =========================================================
    // BETŰTÍPUS
    // =========================================================

    private void FontFamilyComboBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (FontFamilyComboBox.SelectedItem
            is System.Windows.Controls.ComboBoxItem item)
        {
            var font =
                item.Content?.ToString();

            if (!string.IsNullOrWhiteSpace(
                font))
            {
                SaveReadingPosition();

                readerFontFamily =
                    font;

                RefreshCurrentChapter();
            }
        }
    }


    // =========================================================
    // SORKÖZ
    // =========================================================

    private void LineSpacingComboBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LineSpacingComboBox.SelectedItem
            is System.Windows.Controls.ComboBoxItem item)
        {
            var value =
                item.Content?.ToString();

            if (double.TryParse(
                value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var spacing))
            {
                SaveReadingPosition();

                readerLineSpacing =
                    spacing;

                RefreshCurrentChapter();
            }
        }
    }


    // =========================================================
    // VILÁGOS / SÖTÉT MÓD
    // =========================================================

    private void ThemeButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SaveReadingPosition();

        darkMode =
            !darkMode;

        ThemeButton.Content =
            darkMode
                ? "☀️ Világos"
                : "🌙 Sötét";

        RefreshCurrentChapter();
    }


    // =========================================================
    // AKTUÁLIS FEJEZET FRISSÍTÉSE
    // =========================================================

    private void RefreshCurrentChapter()
    {
        if (ChapterList.SelectedItem is Chapter chapter)
        {
            ShowChapter(
                chapter);
        }
    }


    // =========================================================
    // OLVASÁSI POZÍCIÓ KÖVETÉSE
    // =========================================================

    private void StartReadingPositionTracking()
    {
        readingPositionTimer.Stop();

        readingPositionTimer.Start();
    }


    private void StopReadingPositionTracking()
    {
        readingPositionTimer.Stop();
    }


    // =========================================================
    // IDŐZÍTETT MENTÉS
    // =========================================================

    private async void ReadingPositionTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (restoringReadingPosition)
        {
            return;
        }

        await SaveReadingPositionAsync();
    }


    // =========================================================
    // OLVASÁSI POZÍCIÓ MENTÉSE
    // =========================================================

    private void SaveReadingPosition()
    {
        try
        {
            if (contentViewer == null ||
                contentViewer.CoreWebView2 == null)
            {
                return;
            }

            if (currentBook == null ||
                currentChapter == null)
            {
                return;
            }

            var script =
                "JSON.stringify({" +
                "scrollY: window.scrollY," +
                "scrollHeight: document.documentElement.scrollHeight," +
                "clientHeight: document.documentElement.clientHeight" +
                "})";

            var task =
                contentViewer.ExecuteScriptAsync(
                    script);

            task.ContinueWith(
                t =>
                {
                    if (t.IsFaulted ||
                        t.IsCanceled)
                    {
                        return;
                    }

                    Dispatcher.Invoke(
                        () =>
                        {
                            try
                            {
                                SavePositionFromJson(
                                    t.Result);
                            }
                            catch
                            {
                            }
                        });
                });
        }
        catch
        {
        }
    }


    // =========================================================
    // ASZINKRON POZÍCIÓMENTÉS
    // =========================================================

    private async System.Threading.Tasks.Task
        SaveReadingPositionAsync()
    {
        try
        {
            if (contentViewer == null ||
                contentViewer.CoreWebView2 == null)
            {
                return;
            }

            if (currentBook == null ||
                currentChapter == null)
            {
                return;
            }

            var script =
                "JSON.stringify({" +
                "scrollY: window.scrollY," +
                "scrollHeight: document.documentElement.scrollHeight," +
                "clientHeight: document.documentElement.clientHeight" +
                "})";

            var result =
                await contentViewer.ExecuteScriptAsync(
                    script);

            SavePositionFromJson(
                result);
        }
        catch
        {
        }
    }


    // =========================================================
    // POZÍCIÓ FELDOLGOZÁSA
    // =========================================================

    private void SavePositionFromJson(
        string json)
    {
        try
        {
            if (currentBook == null ||
                currentChapter == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                json))
            {
                return;
            }

            var cleaned =
                json.Trim('"')
                    .Replace(
                        "\\\"",
                        "\"");

            using var document =
                System.Text.Json.JsonDocument.Parse(
                    cleaned);

            var root =
                document.RootElement;

            if (!root.TryGetProperty(
                "scrollY",
                out var scrollYElement))
            {
                return;
            }

            var scrollY =
                scrollYElement.GetDouble();

            var chapterPath =
                GetChapterPath(
                    currentChapter);

            library.UpdateReadingPosition(
                currentBook,
                chapterPath,
                scrollY);

            currentBook.LastChapterPath =
                chapterPath;

            currentBook.LastScrollPosition =
                scrollY;

            currentBook.LastOpened =
                DateTime.Now;
        }
        catch
        {
        }
    }


    // =========================================================
    // FEJEZET ÚTVONAL
    // =========================================================

    private static string GetChapterPath(
        Chapter chapter)
    {
        if (!string.IsNullOrWhiteSpace(
            chapter.FilePath))
        {
            return chapter.FilePath;
        }

        return chapter.Href ?? string.Empty;
    }


    // =========================================================
    // UTOLSÓ OLVASÁSI HELY KERESÉSE
    // =========================================================

    private void RestoreLastReadingPosition(
        Book book)
    {
        if (string.IsNullOrWhiteSpace(
            book.LastChapterPath))
        {
            return;
        }

        var targetPath =
            NormalizeChapterPath(
                book.LastChapterPath);

        var chapter =
            book.Chapters.FirstOrDefault(
                c =>
                    string.Equals(
                        NormalizeChapterPath(
                            c.FilePath),
                        targetPath,
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        NormalizeChapterPath(
                            c.Href),
                        targetPath,
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        Path.GetFileName(
                            c.FilePath),
                        Path.GetFileName(
                            targetPath),
                        StringComparison.OrdinalIgnoreCase));

        if (chapter == null)
        {
            return;
        }

        restoringReadingPosition =
            true;

        ChapterList.SelectedItem =
            chapter;

        ChapterList.ScrollIntoView(
            chapter);

        currentBook =
            book;

        currentChapter =
            chapter;

        ShowChapter(
            chapter);
    }


    // =========================================================
    // MENTETT GÖRGETÉSI POZÍCIÓ VISSZAÁLLÍTÁSA
    // =========================================================

    private async System.Threading.Tasks.Task
        ApplySavedScrollPosition()
    {
        if (currentBook == null)
        {
            restoringReadingPosition =
                false;

            return;
        }

        var position =
            currentBook.LastScrollPosition;

        if (position <= 0)
        {
            restoringReadingPosition =
                false;

            StartReadingPositionTracking();

            return;
        }

        try
        {
            var script =
                "window.scrollTo(0, " +
                position.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                ");";

            await contentViewer.ExecuteScriptAsync(
                script);
        }
        catch
        {
        }

        restoringReadingPosition =
            false;

        StartReadingPositionTracking();
    }


    // =========================================================
    // WEBVIEW2 BETÖLTÉS UTÁN
    // =========================================================

    private async void
        ContentViewer_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(
            pendingFragment))
        {
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
            }

            StartReadingPositionTracking();

            return;
        }

        if (restoringReadingPosition)
        {
            await ApplySavedScrollPosition();

            return;
        }

        StartReadingPositionTracking();
    }


    // =========================================================
    // BELSŐ EPUB LINK
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
            SaveReadingPosition();

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
    // PROGRAM BEZÁRÁSA
    // =========================================================

    private void MainWindow_Closing(
        object? sender,
        System.ComponentModel.CancelEventArgs e)
    {
        StopReadingPositionTracking();

        SaveReadingPosition();
    }


    // =========================================================
    // JAVASCRIPT STRING ESCAPELÉS
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
            {
                continue;
            }

            if (part == "..")
            {
                if (result.Count > 0)
                {
                    result.RemoveAt(
                        result.Count - 1);
                }

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