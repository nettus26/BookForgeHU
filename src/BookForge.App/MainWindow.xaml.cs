using BookForge.App.Services;
using BookForge.Core.Models;
using BookForge.Epub;
using BookForge.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BookForge.App;


public sealed class BookProgressTextConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is Book book &&
            book.Chapters != null)
        {
            var countedChapters =
                book.Chapters
                    .Where(ChapterRules.IsCountedChapter)
                    .ToList();

            var total =
                countedChapters.Count;

            var read =
                countedChapters.Count(
                    c => c.IsRead);

            return $"{read} / {total} fejezet";
        }

        return "0 / 0 fejezet";
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class BookProgressPercentConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is Book book &&
            book.Chapters != null &&
            book.Chapters.Count > 0)
        {
            var countedChapters =
                book.Chapters
                    .Where(ChapterRules.IsCountedChapter)
                    .ToList();

            var total =
                countedChapters.Count;

            var read =
                countedChapters.Count(
                    c => c.IsRead);

            return total > 0
                ? Math.Round(
                    read * 100.0 / total)
                : 0.0;
        }

        return 0.0;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

internal static class ChapterRules
{
    public static bool IsCountedChapter(
        Chapter chapter)
    {
        if (chapter == null ||
            string.IsNullOrWhiteSpace(chapter.Title))
        {
            return false;
        }

        var title =
            chapter.Title.Trim();

        // A számmal kezdődő címek valódi fejezetek.
        if (title.Length > 0 &&
            char.IsDigit(title[0]))
        {
            return true;
        }

        // Magyar és angol fejezetjelölések.
        if (title.Contains(
                "fejezet",
                StringComparison.OrdinalIgnoreCase)
            ||
            title.Contains(
                "chapter",
                StringComparison.OrdinalIgnoreCase)
            ||
            title.Contains(
                "rész",
                StringComparison.OrdinalIgnoreCase)
            ||
            title.Contains(
                "epilógus",
                StringComparison.OrdinalIgnoreCase)
            ||
            title.Contains(
                "epilogue",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Például a "Nova" jellegű külön elem
        // nem számít olvasandó fejezetnek.
        return false;
    }
}


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

    private double readerLineSpacing = 1.5;

    private bool darkMode = false;


    // =========================================================
    // OLVASÁSI POZÍCIÓ
    // =========================================================

    private readonly DispatcherTimer readingPositionTimer;

    private Book? currentBook;

    private Chapter? currentChapter;

    private bool restoringReadingPosition = false;

    private bool restoringReaderSettings = false;


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

                    // Az EPUB újratöltésekor a fejezetek új példányok lesznek,
                    // ezért a mentett olvasottsági állapotokat külön vissza kell másolni.
                    if (savedBook.Chapters != null &&
                        fullBook.Chapters != null)
                    {
                        foreach (var savedChapter in savedBook.Chapters)
                        {
                            var matchingChapter =
                                fullBook.Chapters.FirstOrDefault(
                                    chapter =>
                                        string.Equals(
                                            GetChapterPath(chapter),
                                            GetChapterPath(savedChapter),
                                            StringComparison.OrdinalIgnoreCase));

                            if (matchingChapter != null)
                            {
                                matchingChapter.IsRead =
                                    savedChapter.IsRead;
                            }
                        }
                    }

                    fullBook.ReaderFontSize =
                        savedBook.ReaderFontSize > 0
                            ? savedBook.ReaderFontSize
                            : 20;

                    fullBook.ReaderFontFamily =
                        string.IsNullOrWhiteSpace(
                            savedBook.ReaderFontFamily)
                            ? "Georgia"
                            : savedBook.ReaderFontFamily;

                    fullBook.ReaderLineSpacing =
                        savedBook.ReaderLineSpacing > 0
                            ? savedBook.ReaderLineSpacing
                            : 1.5;

                    fullBook.ReaderDarkMode =
                        savedBook.ReaderDarkMode;

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

        RefreshLibraryList();
    }


    // =========================================================
    // KÖNYVTÁRI KERESÉS ÉS RENDEZÉS
    // =========================================================

    private void LibrarySearchBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if (BookList == null)
        {
            return;
        }

        RefreshLibraryList();
    }


    private void LibrarySortComboBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (BookList == null)
        {
            return;
        }

        RefreshLibraryList();
    }


    // =========================================================
    // KÖNYVTÁRI NÉZETVÁLTÁS
    // =========================================================

    private bool isLibraryListView = false;


    private void LibraryViewToggleButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        isLibraryListView =
            !isLibraryListView;

        if (isLibraryListView)
        {
            BookList.ItemTemplate =
                (DataTemplate)FindResource(
                    "LibraryListTemplate");

            BookList.ItemsPanel =
                new ItemsPanelTemplate(
                    new FrameworkElementFactory(
                        typeof(StackPanel)));

            LibraryViewToggleButton.Content =
                "▦ Rács nézet";
        }
        else
        {
            BookList.ItemTemplate =
                (DataTemplate)FindResource(
                    "LibraryGridTemplate");

            var panelFactory =
                new FrameworkElementFactory(
                    typeof(UniformGrid));

            panelFactory.SetValue(
                UniformGrid.ColumnsProperty,
                2);

            BookList.ItemsPanel =
                new ItemsPanelTemplate(
                    panelFactory);

            LibraryViewToggleButton.Content =
                "☰ Lista nézet";
        }

        RefreshLibraryList();
    }


    // =========================================================
    // KEDVENCEK
    // =========================================================

    private bool showFavoritesOnly = false;


    private void LibraryFavoritesToggleButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        showFavoritesOnly =
            !showFavoritesOnly;

        LibraryFavoritesToggleButton.Content =
            showFavoritesOnly
                ? "★ Kedvencek"
                : "☆ Kedvencek";

        RefreshLibraryList();
    }


    private void ToggleSelectedBookFavorite()
    {
        if (BookList.SelectedItem is not Book book)
        {
            return;
        }

        book.IsFavorite =
            !book.IsFavorite;

        RefreshLibraryList();
    }


    private void BookList_MouseDoubleClick(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        ToggleSelectedBookFavorite();
    }


    // =========================================================
    // OLVASÁSI ÁLLAPOT SZŰRÉSE
    // =========================================================

    private void LibraryStatusFilterComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        RefreshLibraryList();
    }


    private string GetSelectedLibraryStatusFilter()
    {
        return
            (LibraryStatusFilterComboBox?.SelectedItem
                as ComboBoxItem)
                ?.Content?.ToString()
            ?? "Minden állapot";
    }


    private void RefreshLibraryList()
    {
        if (BookList == null)
        {
            return;
        }

        var searchText =
            LibrarySearchBox?.Text?.Trim() ?? string.Empty;

        var selectedBook =
            BookList.SelectedItem as Book;

        IEnumerable<Book> filteredBooks =
            books;

        if (showFavoritesOnly)
        {
            filteredBooks =
                filteredBooks.Where(
                    book => book.IsFavorite);
        }

        var statusFilter =
            GetSelectedLibraryStatusFilter();

        if (statusFilter != "Minden állapot")
        {
            filteredBooks =
                filteredBooks.Where(
                    book =>
                        statusFilter == "Olvasatlan"
                            ? GetReadingStatus(book) == 0
                            : statusFilter == "Folyamatban"
                                ? GetReadingStatus(book) == 1
                                : GetReadingStatus(book) == 2);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filteredBooks =
                filteredBooks.Where(
                    book =>
                        (!string.IsNullOrWhiteSpace(book.Title) &&
                         book.Title.Contains(
                             searchText,
                             StringComparison.OrdinalIgnoreCase))
                        ||
                        (!string.IsNullOrWhiteSpace(book.Author) &&
                         book.Author.Contains(
                             searchText,
                             StringComparison.OrdinalIgnoreCase)));
        }

        var sortMode =
            (LibrarySortComboBox?.SelectedItem
                as System.Windows.Controls.ComboBoxItem)
                ?.Content?.ToString()
            ?? "Alapértelmezett";

        filteredBooks =
            sortMode switch
            {
                "Cím szerint" =>
                    filteredBooks
                        .OrderBy(
                            book => book.Title ?? string.Empty,
                            StringComparer.CurrentCultureIgnoreCase),

                "Szerző szerint" =>
                    filteredBooks
                        .OrderBy(
                            book => book.Author ?? string.Empty,
                            StringComparer.CurrentCultureIgnoreCase),

                "Legutóbb olvasott" =>
                    filteredBooks
                        .OrderByDescending(
                            book => book.LastOpened),

                "Olvasatlan" =>
                    filteredBooks
                        .OrderBy(
                            GetReadingStatus),

                "Folyamatban" =>
                    filteredBooks
                        .OrderBy(
                            GetInProgressSortValue),

                "Befejezett" =>
                    filteredBooks
                        .OrderByDescending(
                            GetCompletedSortValue),

                _ =>
                    filteredBooks
            };

        var result =
            filteredBooks.ToList();

        BookList.ItemsSource = null;
        BookList.Items.Clear();

        foreach (var book in result)
        {
            BookList.Items.Add(book);
        }

        if (LibraryResultCountText != null)
        {
            LibraryResultCountText.Text =
                result.Count == 1
                    ? "1 könyv"
                    : $"{result.Count} könyv";
        }

        if (LibraryNoResultsText != null)
        {
            LibraryNoResultsText.Visibility =
                result.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        if (selectedBook != null &&
            result.Contains(selectedBook))
        {
            BookList.SelectedItem =
                selectedBook;
        }
    }


    private void ClearLibrarySearchButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (LibrarySearchBox == null)
        {
            return;
        }

        LibrarySearchBox.Clear();
        LibrarySearchBox.Focus();
    }


    private static int GetReadingStatus(
        Book book)
    {
        var total =
            book.Chapters?.Count ?? 0;

        var read =
            book.Chapters?.Count(
                chapter => chapter.IsRead) ?? 0;

        if (total == 0 ||
            read == 0)
        {
            return 0;
        }

        if (read < total)
        {
            return 1;
        }

        return 2;
    }


    private static int GetInProgressSortValue(
        Book book)
    {
        var total =
            book.Chapters?.Count ?? 0;

        var read =
            book.Chapters?.Count(
                chapter => chapter.IsRead) ?? 0;

        if (total > 0 &&
            read > 0 &&
            read < total)
        {
            return 0;
        }

        if (total > 0 &&
            read == total)
        {
            return 1;
        }

        return 2;
    }


    private static int GetCompletedSortValue(
        Book book)
    {
        var total =
            book.Chapters?.Count ?? 0;

        var read =
            book.Chapters?.Count(
                chapter => chapter.IsRead) ?? 0;

        return total > 0 &&
               read == total
            ? 1
            : 0;
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
                var importedBook =
                    importer.ImportEpub(
                        dialog.FileName);

                // Az ImportService már kiszűri a Library-ben
                // létező azonos című és szerzőjű könyvet.
                // Itt azt is biztosítjuk, hogy a képernyőn lévő
                // in-memory lista ne kapjon még egy példányt.
                var existingBook =
                    books.FirstOrDefault(
                        b =>
                            string.Equals(
                                b.Title,
                                importedBook.Title,
                                StringComparison.OrdinalIgnoreCase)
                            &&
                            string.Equals(
                                b.Author,
                                importedBook.Author,
                                StringComparison.OrdinalIgnoreCase));

                Book bookToSelect;

                if (existingBook != null)
                {
                    bookToSelect =
                        existingBook;

                    MessageBox.Show(
                        "Ez a könyv már szerepel a könyvtárban.",
                        "Könyv már létezik",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    books.Add(
                        importedBook);

                    bookToSelect =
                        importedBook;
                }

                RefreshLibraryList();

                BookList.SelectedItem =
                    bookToSelect;

                BookList.ScrollIntoView(
                    bookToSelect);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "EPUB betöltési hiba",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

                RefreshLibraryList();

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

        OpenBook(
            book);
    }


    // =========================================================
    // KÖNYV MEGNYITÁSA AZ OLVASÓBAN
    // =========================================================

    private void OpenBook(
        Book book)
    {
        if (book == null)
        {
            return;
        }

        // Az előző könyv pozícióját még a váltás előtt mentjük.
        SaveReadingPosition();

        StopReadingPositionTracking();

        restoringReadingPosition =
            false;

        currentBook =
            book;

        currentChapter =
            null;

        // A könyv megnyitását azonnal rögzítjük.
        currentBook.LastOpened =
            DateTime.Now;

        library.UpdateLastOpened(
            currentBook);

        BookTitleText.Text =
            book.Title;

        BookAuthorText.Text =
            book.Author;

        BookLanguageText.Text =
            book.Language;

        BookDateText.Text =
            book.CreatedDate.ToString(
                "yyyy.MM.dd.");

        UpdateBookStatistics(
            book);

        ApplyBookReaderSettings(
            book);

        LoadCover(
            book);

        LoadTableOfContents(
            book);

        LoadChapterList(
            book);

        // Ha van mentett hely, azt állítjuk vissza.
        if (!string.IsNullOrWhiteSpace(
            book.LastChapterPath))
        {
            RestoreLastReadingPosition(
                book);

            return;
        }

        // Új könyvnél automatikusan az első fejezet nyílik meg.
        var firstChapter =
            book.Chapters?
                .OrderBy(
                    chapter => chapter.Order)
                .FirstOrDefault();

        if (firstChapter == null)
        {
            contentViewer.NavigateToString(
                CreateReaderHtml(
                    "<p>Ehhez a könyvhöz nem található olvasható fejezet.</p>"));

            return;
        }

        ChapterList.SelectedItem =
            firstChapter;

        ChapterList.ScrollIntoView(
            firstChapter);

        currentChapter =
            firstChapter;

        ShowChapter(
            firstChapter);
    }


    // =========================================================
    // FOLYTATÁS
    // =========================================================

    private void ContinueReadingButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (currentBook == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
            currentBook.LastChapterPath))
        {
            MessageBox.Show(
                "Ehhez a könyvhöz még nincs mentett olvasási hely.",
                "BookForge");
            return;
        }

        RestoreLastReadingPosition(
            currentBook);
    }


    // =========================================================
    // KÖNYV STATISZTIKÁK
    // =========================================================

    private void UpdateBookStatistics(
        Book book)
    {
        var countedChapters =
            book.Chapters?
                .Where(ChapterRules.IsCountedChapter)
                .ToList()
            ?? new List<Chapter>();

        var totalChapters =
            countedChapters.Count;

        var readChapters =
            countedChapters.Count(
                chapter => chapter.IsRead);

        BookChapterCountText.Text =
            totalChapters.ToString();

        BookReadCountText.Text =
            $"{readChapters} / {totalChapters}";

        var progress =
            totalChapters > 0
                ? (int)Math.Round(
                    readChapters * 100.0 / totalChapters)
                : 0;

        BookProgressText.Text =
            $"{progress}%";

        BookProgressBar.Value =
            progress;
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

                if (!IsBlackPlaceholder(image))
                {
                    CoverImageBox.Source =
                        image;

                    return;
                }
            }

            CoverImageBox.Source =
                BookCoverImageConverter
                    .CreateBookForgeCover();
        }
        catch
        {
            CoverImageBox.Source =
                BookCoverImageConverter
                    .CreateBookForgeCover();
        }
    }


    private static bool IsBlackPlaceholder(
        BitmapSource image)
    {
        try
        {
            var converted =
                new FormatConvertedBitmap(
                    image,
                    PixelFormats.Bgra32,
                    null,
                    0);

            var width =
                converted.PixelWidth;

            var height =
                converted.PixelHeight;

            if (width <= 0 || height <= 0)
            {
                return true;
            }

            var stride =
                width * 4;

            var pixels =
                new byte[
                    stride * height];

            converted.CopyPixels(
                pixels,
                stride,
                0);

            long totalBrightness = 0;
            int samples = 0;

            var stepX =
                Math.Max(1, width / 20);

            var stepY =
                Math.Max(1, height / 20);

            for (var y = 0;
                 y < height;
                 y += stepY)
            {
                for (var x = 0;
                     x < width;
                     x += stepX)
                {
                    var index =
                        (y * stride) +
                        (x * 4);

                    var blue =
                        pixels[index];

                    var green =
                        pixels[index + 1];

                    var red =
                        pixels[index + 2];

                    totalBrightness +=
                        red + green + blue;

                    samples++;
                }
            }

            if (samples == 0)
            {
                return true;
            }

            var averageBrightness =
                totalBrightness /
                (double)(samples * 3);

            return averageBrightness < 8.0;
        }
        catch
        {
            return false;
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

        // A Tartalomjegyzék a feldolgozott fejezetekből készül.
        // Nem korlátozzuk a címeket számjeggyel kezdődő
        // fejezetekre, mert sok EPUB-ban a fejezetcímek
        // nem számozottak.
        var entries =
            book.Chapters
                .Where(
                    chapter =>
                        chapter != null &&
                        !string.IsNullOrWhiteSpace(
                            chapter.Title) &&
                        !string.Equals(
                            chapter.Title.Trim(),
                            "Cover",
                            StringComparison.OrdinalIgnoreCase))
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
        StopReadingPositionTracking();

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
    // KÖNYV OLVASÓBEÁLLÍTÁSAINAK BETÖLTÉSE
    // =========================================================

    private void ApplyBookReaderSettings(
        Book book)
    {
        restoringReaderSettings = true;

        try
        {
            readerFontSize =
                book.ReaderFontSize > 0
                    ? book.ReaderFontSize
                    : 20;

            readerFontFamily =
                string.IsNullOrWhiteSpace(
                    book.ReaderFontFamily)
                    ? "Georgia"
                    : book.ReaderFontFamily;

            readerLineSpacing =
                book.ReaderLineSpacing > 0
                    ? book.ReaderLineSpacing
                    : 1.5;

            darkMode =
                book.ReaderDarkMode;

            UpdateFontSizeDisplay();

            SelectComboBoxItem(
                FontFamilyComboBox,
                readerFontFamily);

            SelectComboBoxItem(
                LineSpacingComboBox,
                readerLineSpacing.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));

            ThemeButton.Content =
                darkMode
                    ? "☀️ Világos"
                    : "🌙 Sötét";
        }
        finally
        {
            restoringReaderSettings = false;
        }
    }

    private static void SelectComboBoxItem(
        System.Windows.Controls.ComboBox comboBox,
        string value)
    {
        foreach (var item in comboBox.Items)
        {
            if (item is System.Windows.Controls.ComboBoxItem comboItem &&
                string.Equals(
                    comboItem.Content?.ToString(),
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = comboItem;
                return;
            }
        }
    }

    private void SaveCurrentReaderSettings()
    {
        if (restoringReaderSettings ||
            currentBook == null)
        {
            return;
        }

        currentBook.ReaderFontSize = readerFontSize;
        currentBook.ReaderFontFamily = readerFontFamily;
        currentBook.ReaderLineSpacing = readerLineSpacing;
        currentBook.ReaderDarkMode = darkMode;

        library.UpdateReaderSettings(
            currentBook,
            readerFontSize,
            readerFontFamily,
            readerLineSpacing,
            darkMode);
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

        SaveCurrentReaderSettings();
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

        SaveCurrentReaderSettings();
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

                SaveCurrentReaderSettings();
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

                SaveCurrentReaderSettings();
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

        SaveCurrentReaderSettings();
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
                "scrollY: Math.max(" +
                    "window.scrollY || 0," +
                    "document.documentElement.scrollTop || 0," +
                    "document.body ? document.body.scrollTop || 0 : 0" +
                ")," +
                "scrollHeight: Math.max(" +
                    "document.documentElement.scrollHeight || 0," +
                    "document.body ? document.body.scrollHeight || 0 : 0" +
                ")," +
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
                "scrollY: Math.max(" +
                    "window.scrollY || 0," +
                    "document.documentElement.scrollTop || 0," +
                    "document.body ? document.body.scrollTop || 0 : 0" +
                ")," +
                "scrollHeight: Math.max(" +
                    "document.documentElement.scrollHeight || 0," +
                    "document.body ? document.body.scrollHeight || 0 : 0" +
                ")," +
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

            var scrollHeight =
                root.TryGetProperty(
                    "scrollHeight",
                    out var scrollHeightElement)
                    ? scrollHeightElement.GetDouble()
                    : 0;

            var clientHeight =
                root.TryGetProperty(
                    "clientHeight",
                    out var clientHeightElement)
                    ? clientHeightElement.GetDouble()
                    : 0;

            // A fejezet végét elértük, ha a görgetés az alsó részhez ért.
            var reachedChapterEnd =
                scrollHeight > 0 &&
                clientHeight > 0 &&
                scrollY + clientHeight >= scrollHeight - 50;

            if (reachedChapterEnd &&
                !currentChapter.IsRead)
            {
                currentChapter.IsRead = true;

                UpdateBookStatistics(
                    currentBook);

                ChapterList.Items.Refresh();
            }

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
            // A NavigateToString után a DOM már létrejött, de a hosszú
            // szöveg magassága még egy rövid ideig változhat.
            // Ezért várunk egy kicsit, majd több lépésben állítjuk vissza.
            await System.Threading.Tasks.Task.Delay(100);

            var positionText =
                position.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);

            var script =
                "(function() {" +
                "var y = " + positionText + ";" +
                "window.scrollTo(0, y);" +
                "if (document.documentElement) " +
                    "document.documentElement.scrollTop = y;" +
                "if (document.body) " +
                    "document.body.scrollTop = y;" +
                "requestAnimationFrame(function() {" +
                    "window.scrollTo(0, y);" +
                    "if (document.documentElement) " +
                        "document.documentElement.scrollTop = y;" +
                    "if (document.body) " +
                        "document.body.scrollTop = y;" +
                "});" +
                "})();";

            await contentViewer.ExecuteScriptAsync(
                script);

            await System.Threading.Tasks.Task.Delay(150);

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

// =========================================================
// KÖNYVBORÍTÓ KONVERTER
// =========================================================

public class BookCoverImageConverter :
    IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is not Book book)
        {
            return CreateBookForgeCover();
        }

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

                if (!IsBlackPlaceholder(image))
                {
                    return image;
                }
            }
        }
        catch
        {
            // A BookForge alapértelmezett borítójára esünk vissza.
        }

        return CreateBookForgeCover();
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static bool IsBlackPlaceholder(
        BitmapSource image)
    {
        try
        {
            var converted =
                new FormatConvertedBitmap(
                    image,
                    PixelFormats.Bgra32,
                    null,
                    0);

            var width =
                converted.PixelWidth;

            var height =
                converted.PixelHeight;

            if (width <= 0 || height <= 0)
            {
                return true;
            }

            var stride =
                width * 4;

            var pixels =
                new byte[
                    stride * height];

            converted.CopyPixels(
                pixels,
                stride,
                0);

            long totalBrightness = 0;
            int samples = 0;

            var stepX =
                Math.Max(1, width / 20);

            var stepY =
                Math.Max(1, height / 20);

            for (var y = 0;
                 y < height;
                 y += stepY)
            {
                for (var x = 0;
                     x < width;
                     x += stepX)
                {
                    var index =
                        (y * stride) +
                        (x * 4);

                    totalBrightness +=
                        pixels[index] +
                        pixels[index + 1] +
                        pixels[index + 2];

                    samples++;
                }
            }

            if (samples == 0)
            {
                return true;
            }

            var averageBrightness =
                totalBrightness /
                (double)(samples * 3);

            return averageBrightness < 8.0;
        }
        catch
        {
            return false;
        }
    }

    public static ImageSource CreateBookForgeCover()
    {
        const int width = 420;
        const int height = 620;

        var visual =
            new DrawingVisual();

        using (var context =
            visual.RenderOpen())
        {
            var backgroundBrush =
                new SolidColorBrush(
                    Color.FromRgb(18, 35, 52));

            var borderBrush =
                new SolidColorBrush(
                    Color.FromRgb(150, 125, 82));

            var goldBrush =
                new SolidColorBrush(
                    Color.FromRgb(218, 192, 140));

            var lightBrush =
                new SolidColorBrush(
                    Color.FromRgb(232, 232, 232));

            context.DrawRectangle(
                backgroundBrush,
                null,
                new Rect(
                    0,
                    0,
                    width,
                    height));

            var outerPen =
                new Pen(
                    borderBrush,
                    5);

            var innerPen =
                new Pen(
                    borderBrush,
                    2);

            context.DrawRectangle(
                null,
                outerPen,
                new Rect(
                    18,
                    18,
                    width - 36,
                    height - 36));

            context.DrawRectangle(
                null,
                innerPen,
                new Rect(
                    32,
                    32,
                    width - 64,
                    height - 64));

            var titleTypeface =
                new Typeface(
                    new FontFamily("Georgia"),
                    FontStyles.Normal,
                    FontWeights.Bold,
                    FontStretches.Normal);

            var subtitleTypeface =
                new Typeface(
                    new FontFamily("Georgia"),
                    FontStyles.Normal,
                    FontWeights.Normal,
                    FontStretches.Normal);

            var title =
                new FormattedText(
                    "BOOKFORGE",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    titleTypeface,
                    30,
                    goldBrush,
                    1.0);

            title.TextAlignment =
                TextAlignment.Center;

            context.DrawText(
                title,
                new Point(
                    (width - title.Width) / 2,
                    125));

            var subtitle =
                new FormattedText(
                    "NINCS BORÍTÓ",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    subtitleTypeface,
                    24,
                    lightBrush,
                    1.0);

            subtitle.TextAlignment =
                TextAlignment.Center;

            context.DrawText(
                subtitle,
                new Point(
                    (width - subtitle.Width) / 2,
                    175));

            var bookPen =
                new Pen(
                    goldBrush,
                    5);

            var centerX =
                width / 2.0;

            var bookTop = 270.0;
            var bookBottom = 390.0;
            var bookLeft = 105.0;
            var bookRight = 315.0;

            var leftGeometry =
                new StreamGeometry();

            using (var geometryContext =
                leftGeometry.Open())
            {
                geometryContext.BeginFigure(
                    new Point(
                        centerX,
                        bookTop),
                    true,
                    false);

                geometryContext.LineTo(
                    new Point(
                        bookLeft,
                        bookTop + 30),
                    true,
                    false);

                geometryContext.LineTo(
                    new Point(
                        bookLeft,
                        bookBottom),
                    true,
                    false);

                geometryContext.LineTo(
                    new Point(
                        centerX,
                        bookBottom - 28),
                    true,
                    false);

                geometryContext.Close();
            }

            var rightGeometry =
                new StreamGeometry();

            using (var geometryContext =
                rightGeometry.Open())
            {
                geometryContext.BeginFigure(
                    new Point(
                        centerX,
                        bookTop),
                    true,
                    false);

                geometryContext.LineTo(
                    new Point(
                        bookRight,
                        bookTop + 30),
                    true,
                    false);

                geometryContext.LineTo(
                    new Point(
                        bookRight,
                        bookBottom),
                    true,
                    false);

                geometryContext.LineTo(
                    new Point(
                        centerX,
                        bookBottom - 28),
                    true,
                    false);

                geometryContext.Close();
            }

            context.DrawGeometry(
                null,
                bookPen,
                leftGeometry);

            context.DrawGeometry(
                null,
                bookPen,
                rightGeometry);

            context.DrawLine(
                bookPen,
                new Point(
                    centerX,
                    bookTop),
                new Point(
                    centerX,
                    bookBottom));

            var ornament =
                new FormattedText(
                    "✦",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    subtitleTypeface,
                    28,
                    goldBrush,
                    1.0);

            ornament.TextAlignment =
                TextAlignment.Center;

            context.DrawText(
                ornament,
                new Point(
                    (width - ornament.Width) / 2,
                    455));

            var bottomLinePen =
                new Pen(
                    borderBrush,
                    2);

            context.DrawLine(
                bottomLinePen,
                new Point(105, 505),
                new Point(315, 505));
        }

        var bitmap =
            new RenderTargetBitmap(
                width,
                height,
                96,
                96,
                PixelFormats.Pbgra32);

        bitmap.Render(
            visual);

        return bitmap;
    }
}
