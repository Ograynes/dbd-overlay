using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DBDOverlay.Core.ImageProcessing
{
    public static class GameCapture
    {
        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
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
                var hwnd = FindGameWindow(requireForeground);
                if (hwnd == IntPtr.Zero || IsIconic(hwnd) || (requireForeground && hwnd != GetForegroundWindow())) return false;
                if (!GetClientRect(hwnd, out var rect)) return false;
                var origin = new Point();
                if (!ClientToScreen(hwnd, ref origin) || rect.Right <= 0 || rect.Bottom <= 0) return false;
                bounds = new Rectangle(origin.X, origin.Y, rect.Right, rect.Bottom);
                return true;
            }
        }

        private static IntPtr FindGameWindow(bool requireForeground)
        {
            // The game's window title can contain trailing spaces or change between builds.
            // Identify its process instead; never require access to the protected main module.
            var foreground = GetForegroundWindow();
            foreach (var name in new[] { "DeadByDaylight-Win64-Shipping", "DeadByDaylight-EGS-Shipping" })
            {
                var processes = Process.GetProcessesByName(name);
                try
                {
                    foreach (var process in processes)
                    {
                        try
                        {
                            var window = process.MainWindowHandle;
                            if (window != IntPtr.Zero && !IsIconic(window) && (!requireForeground || window == foreground))
                                return window;
                        }
                        catch (InvalidOperationException) { }
                        catch (System.ComponentModel.Win32Exception) { }
                    }
                }
                finally { foreach (var process in processes) process.Dispose(); }
            }
            return IntPtr.Zero;
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
