using System;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace DBDOverlay.Core.ImageProcessing
{
    // Coordinates are normalized to the game's client area, never the primary monitor.
    public sealed class HudGeometry
    {
        public RectangleF Region { get; }
        public HudGeometry(RectangleF region)
        {
            if (new[] { region.X, region.Y, region.Width, region.Height }.Any(v => float.IsNaN(v) || float.IsInfinity(v)) ||
                region.X < 0 || region.Y < 0 || region.Width <= 0 || region.Height <= 0 ||
                region.Right > 1.00001f || region.Bottom > 1.00001f)
                throw new ArgumentOutOfRangeException(nameof(region));
            Region = region;
        }

        public static HudGeometry Default(Size client, bool eight)
        {
            double referenceWidth = Math.Min(client.Width, client.Height * 16.0 / 9.0);
            double x = eight ? .048 + .03 * .134 : .04 + .055 * .32;
            double w = eight ? .03 * .64 : .055 * .31;
            double row = eight ? .524 / 8 : .326 / 4;
            double y = (eight ? .186 : .38) + row * (eight ? .236 : .25);
            double h = row * (eight ? .522 : .5);
            return new HudGeometry(new RectangleF((float)(x * referenceWidth / client.Width), (float)y,
                (float)(w * referenceWidth / client.Width), (float)(row * (eight ? 7 : 3) + h)));
        }

        public Rectangle[] Cells(Rectangle client, int count)
        {
            if (count != 4 && count != 8) throw new ArgumentOutOfRangeException(nameof(count));
            double width = Region.Width * client.Width;
            // The reference icon aspect ratio is 33 x 44 for 1v4. 2v8 uses near-square cells.
            double height = Math.Max(1, Math.Floor(Math.Min(Region.Height * client.Height / count, width * (count == 4 ? 44.0 / 33.0 : 1.0))));
            double step = (Region.Height * client.Height - height) / (count - 1);
            return Enumerable.Range(0, count).Select(i => Rectangle.Intersect(client,
                new Rectangle(client.Left + (int)Math.Round(Region.X * client.Width),
                    client.Top + (int)Math.Round(Region.Y * client.Height + step * i),
                    Math.Max(1, (int)Math.Round(width)), Math.Max(1, (int)Math.Round(height))))).ToArray();
        }

        public override string ToString() => string.Join(",", new[] { Region.X, Region.Y, Region.Width, Region.Height }
            .Select(x => x.ToString("R", CultureInfo.InvariantCulture)));
        public static bool TryParse(string text, out HudGeometry geometry)
        {
            geometry = null;
            var parts = (text ?? "").Split(',');
            if (parts.Length != 4) return false;
            var values = new float[4];
            for (int i = 0; i < 4; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return false;
            try { geometry = new HudGeometry(new RectangleF(values[0], values[1], values[2], values[3])); return true; }
            catch (ArgumentOutOfRangeException) { return false; }
        }
    }
}
