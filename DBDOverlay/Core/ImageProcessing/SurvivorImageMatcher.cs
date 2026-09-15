using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DBDOverlay.Core.ImageProcessing
{
    public static class SurvivorImageMatcher
    {
        // Foreground Dice overlap: a mostly black image cannot match a mostly black template.
        public static double Match(Bitmap image, Bitmap reference, int threshold = 600)
        {
            using (var normalized = new Bitmap(reference.Width, reference.Height))
            {
                using (var g = Graphics.FromImage(normalized))
                {
                    // Bicubic downsampling preserves the thin white strokes of the shipped icons.
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(image, new Rectangle(0, 0, normalized.Width, normalized.Height),
                        0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
                }
                int actual = 0, expected = 0, overlap = 0;
                for (int y = 0; y < reference.Height; y++)
                    for (int x = 0; x < reference.Width; x++)
                    {
                        var a = normalized.GetPixel(x, y); var b = reference.GetPixel(x, y);
                        bool onA = a.R + a.G + a.B >= threshold;
                        bool onB = b.R + b.G + b.B >= 400;
                        if (onA) actual++;
                        if (onB) expected++;
                        if (onA && onB) overlap++;
                    }
                if (actual < 4 || expected < 4 || actual == reference.Width * reference.Height || expected == reference.Width * reference.Height) return 0;
                return 2.0 * overlap / (actual + expected);
            }
        }

        // Learned portraits retain color, unlike the monochrome hook templates.
        public static double MatchPortrait(Bitmap image, Bitmap reference)
        {
            using (var a = new Bitmap(image, 32, 40))
            using (var b = new Bitmap(reference, 32, 40))
            {
                double error = 0, mean = 0, squares = 0, actualMean = 0, actualSquares = 0;
                for (int y = 0; y < 40; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        var p = a.GetPixel(x, y); var q = b.GetPixel(x, y);
                        error += Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B);
                        double light = (q.R + q.G + q.B) / 3.0;
                        mean += light; squares += light * light;
                        double actualLight = (p.R + p.G + p.B) / 3.0;
                        actualMean += actualLight; actualSquares += actualLight * actualLight;
                    }
                if (squares / 1280 - Math.Pow(mean / 1280, 2) < 100 ||
                    actualSquares / 1280 - Math.Pow(actualMean / 1280, 2) < 100) return 0;
                return 1 - error / (1280 * 3 * 255);
            }
        }
    }
}
