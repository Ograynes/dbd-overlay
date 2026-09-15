using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DBDOverlay.Core.ImageProcessing
{
    public static class GameCapture
    {
        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

        private sealed class PhysicalPixels : IDisposable
        {
            private readonly IntPtr previous;
            public PhysicalPixels() { previous = SetThreadDpiAwarenessContext(new IntPtr(-4)); }
            public void Dispose() { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
        }

        public static bool TryGetBounds(out Rectangle bounds, bool requireForeground = true)
        {
            bounds = Rectangle.Empty;
            using (new PhysicalPixels())
            {
                var hwnd = FindWindow(null, "DeadByDaylight");
                if (hwnd == IntPtr.Zero || IsIconic(hwnd) || (requireForeground && hwnd != GetForegroundWindow())) return false;
                if (!GetClientRect(hwnd, out var rect)) return false;
                var origin = new Point();
                if (!ClientToScreen(hwnd, ref origin) || rect.Right <= 0 || rect.Bottom <= 0) return false;
                bounds = new Rectangle(origin.X, origin.Y, rect.Right, rect.Bottom);
                return true;
            }
        }

        public static Bitmap Capture(Rectangle area)
        {
            using (new PhysicalPixels())
            {
                var bitmap = new Bitmap(area.Width, area.Height);
                try { using (var g = Graphics.FromImage(bitmap)) g.CopyFromScreen(area.Location, System.Drawing.Point.Empty, area.Size); }
                catch { bitmap.Dispose(); throw; }
                return bitmap;
            }
        }
    }
}
