using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Controls;

namespace BookForge.App.Services;

public class CoverService
{
    public BitmapSource CreateDefaultCover(
        string title,
        string author)
    {
        int width = 600;
        int height = 900;


        var visual = new DrawingVisual();


        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(
                Brushes.White,
                null,
                new Rect(
                    0,
                    0,
                    width,
                    height));


            var titleText = new FormattedText(
                string.IsNullOrWhiteSpace(title)
                    ? "BookForge"
                    : title,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    "Arial",
                    FontStyles.Normal,
                    FontWeights.Bold,
                    FontStretches.Normal),
                42,
                Brushes.Black,
                1.0);


            context.DrawText(
                titleText,
                new Point(
                    70,
                    300));



            var authorText = new FormattedText(
                string.IsNullOrWhiteSpace(author)
                    ? ""
                    : author,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    "Arial",
                    FontStyles.Normal,
                    FontWeights.Normal,
                    FontStretches.Normal),
                28,
                Brushes.Black,
                1.0);


            context.DrawText(
                authorText,
                new Point(
                    70,
                    500));



            var logoText = new FormattedText(
                "📚 BookForge",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    "Arial",
                    FontStyles.Normal,
                    FontWeights.Bold,
                    FontStretches.Normal),
                24,
                Brushes.Gray,
                1.0);


            context.DrawText(
                logoText,
                new Point(
                    70,
                    780));
        }



        var bitmap =
            new RenderTargetBitmap(
                width,
                height,
                96,
                96,
                PixelFormats.Pbgra32);


        bitmap.Render(visual);


        return bitmap;
    }
}