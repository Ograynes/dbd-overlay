using DBDOverlay.Core.ImageProcessing;
using DBDOverlay.Core.Extensions;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using Rectangle = System.Drawing.Rectangle;

namespace DBDOverlay.UI.Windows
{
    public sealed class HudCalibrationWindow : Window
    {
        private readonly Bitmap snapshot;
        private readonly bool eight;
        private readonly Canvas canvas;
        private readonly ComboBox slot;
        private readonly TextBlock status;
        private readonly StackPanel buttons;
        private readonly bool learn;
        private Point start;
        private HudGeometry geometry;
        private WpfRectangle selection;

        public HudCalibrationWindow(Bitmap snapshot, bool eight, bool learn)
        {
            this.snapshot = snapshot; this.eight = eight; this.learn = learn;
            Title = learn ? "Learn an unhooked state" : "Calibrate survivor detection";
            Width = Math.Min(1200, SystemParameters.WorkArea.Width); Height = Math.Min(850, SystemParameters.WorkArea.Height);
            Background = Brushes.DimGray; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var dock = new DockPanel(); Content = dock;
            var help = new TextBlock
            {
                Foreground = Brushes.White,
                Margin = new Thickness(12),
                TextWrapping = TextWrapping.Wrap,
                Text = learn ? "Choose a survivor who is visibly NOT hooked. Save healthy and injured appearances separately. Never learn a hooked, dead, empty or obscured slot. References stay on this PC."
                : "Drag a narrow column from the TOP of the first survivor's central state icon to the BOTTOM of the last. Include all slots, even empty ones. The outlined boxes must cover each state icon; exclude names and decorations. Save, then learn unhooked states."
            };
            DockPanel.SetDock(help, Dock.Top); dock.Children.Add(help);
            buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
            DockPanel.SetDock(buttons, Dock.Bottom); dock.Children.Add(buttons);
            slot = new ComboBox { Width = 130, Margin = new Thickness(5) };
            for (int i = 1; i <= (eight ? 8 : 4); i++) slot.Items.Add("Survivor " + i);
            slot.SelectedIndex = 0;
            if (learn) buttons.Children.Add(slot);
            var save = new Button { Content = learn ? "Save unhooked reference" : "Save region", Margin = new Thickness(5), Padding = new Thickness(8) };
            save.Click += Save; buttons.Children.Add(save);
            var cancel = new Button { Content = "Cancel", Margin = new Thickness(5), Padding = new Thickness(8) };
            cancel.Click += (s, e) => Close(); buttons.Children.Add(cancel);
            status = new TextBlock { Foreground = Brushes.White, Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap, MaxWidth = 400 };
            buttons.Children.Add(status);
            var viewbox = new Viewbox { Stretch = Stretch.Uniform };
            canvas = new Canvas { Width = snapshot.Width, Height = snapshot.Height, Background = Brushes.Black };
            canvas.Children.Add(new System.Windows.Controls.Image { Source = snapshot.ToBitmapImage(), Width = snapshot.Width, Height = snapshot.Height });
            viewbox.Child = canvas; dock.Children.Add(viewbox);
            geometry = DetectionSettings.Default.Geometry(snapshot.Size, eight);
            DrawCells();
            if (!learn)
            {
                canvas.MouseLeftButtonDown += (s, e) => { start = e.GetPosition(canvas); canvas.CaptureMouse(); };
                canvas.MouseMove += (s, e) => { if (canvas.IsMouseCaptured) Select(e.GetPosition(canvas)); };
                canvas.MouseLeftButtonUp += (s, e) => { if (canvas.IsMouseCaptured) { Select(e.GetPosition(canvas)); canvas.ReleaseMouseCapture(); } };
            }
        }

        private void Select(Point end)
        {
            float x = (float)Math.Max(0, Math.Min(start.X, end.X)); float y = (float)Math.Max(0, Math.Min(start.Y, end.Y));
            float right = (float)Math.Min(snapshot.Width, Math.Max(start.X, end.X)); float bottom = (float)Math.Min(snapshot.Height, Math.Max(start.Y, end.Y));
            if (right - x < 8 || bottom - y < 40) return;
            geometry = new HudGeometry(new RectangleF(x / snapshot.Width, y / snapshot.Height, (right - x) / snapshot.Width, (bottom - y) / snapshot.Height));
            DrawCells();
        }

        private void DrawCells()
        {
            while (canvas.Children.Count > 1) canvas.Children.RemoveAt(1);
            foreach (var cell in geometry.Cells(new Rectangle(0, 0, snapshot.Width, snapshot.Height), eight ? 8 : 4))
            {
                selection = new WpfRectangle { Width = cell.Width, Height = cell.Height, Stroke = Brushes.Lime, StrokeThickness = 3, IsHitTestVisible = false };
                Canvas.SetLeft(selection, cell.Left); Canvas.SetTop(selection, cell.Top); canvas.Children.Add(selection);
            }
        }

        private void Save(object sender, RoutedEventArgs e)
        {
            try
            {
                if (learn)
                {
                    var rect = geometry.Cells(new Rectangle(0, 0, snapshot.Width, snapshot.Height), eight ? 8 : 4)[slot.SelectedIndex];
                    using (var image = snapshot.Clone(rect, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) PortraitReferences.Save(image, eight);
                    status.Text = "Saved locally. You can save another visible unhooked slot.";
                }
                else { DetectionSettings.Default.SetRegion(geometry, eight); DialogResult = true; }
            }
            catch (Exception error) { status.Text = error.Message; }
        }
    }
}
