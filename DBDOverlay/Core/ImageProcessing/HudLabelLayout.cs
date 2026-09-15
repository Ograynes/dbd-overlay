using System;
using System.Drawing;

namespace DBDOverlay.Core.ImageProcessing
{
    public static class HudLabelLayout
    {
        public static RectangleF Bounds(HudGeometry geometry, Rectangle client, int count, double dpiX, double dpiY)
        {
            if (dpiX <= 0 || dpiY <= 0) throw new ArgumentOutOfRangeException(nameof(dpiX));
            var cells = geometry.Cells(client, count);
            double first = cells[0].Top + cells[0].Height / 2.0;
            double last = cells[count - 1].Top + cells[count - 1].Height / 2.0;
            double step = (last - first) / (count - 1);
            double iconWidth = Math.Max(cells[0].Width, 72 * dpiX);
            double width = iconWidth / .4; // Match the central column in KillerOverlayWindow.
            double center = cells[0].Left + cells[0].Width / 2.0;
            return new RectangleF((float)((center - .45 * width) / dpiX),
                (float)((first - step / 2) / dpiY), (float)(width / dpiX), (float)(step * count / dpiY));
        }
    }
}
