using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DBDOverlay.UI.Tabs;
using Xunit;

namespace DBDOverlay.Tests
{
    public class UiTests
    {
        [Fact]
        public void ReShadePageLoadsAtNarrowAndWideSizes()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var app = new App(); app.InitializeComponent();
                    var page = new ReshadeTabView();
                    foreach (int width in new[] { 480, 640 })
                    {
                        page.Measure(new Size(width, 900)); page.Arrange(new Rect(0, 0, width, 900)); page.UpdateLayout();
                        var toggle = (FrameworkElement)page.FindName("AutoApplyToggle");
                        var status = (FrameworkElement)page.FindName("ApplyStatus");
                        Assert.True(toggle.ActualWidth > 0); Assert.True(status.ActualWidth > 0);
                        var position = toggle.TranslatePoint(new Point(0, 0), page);
                        Assert.InRange(position.X, 0, width - toggle.ActualWidth);
                        // Optional off-screen render of our own controls, not a desktop screenshot.
                        var output = Environment.GetEnvironmentVariable("DBDOVERLAY_UI_PREVIEW");
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            Directory.CreateDirectory(output);
                            var bitmap = new RenderTargetBitmap(width, 900, 96, 96, PixelFormats.Pbgra32);
                            bitmap.Render(page);
                            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            using (var file = File.Create(Path.Combine(output, "reshade-" + width + ".png"))) encoder.Save(file);
                        }
                    }
                    DBDOverlay.Core.Utils.FileSystem.CreateDefaultFolders();
                    // Construct every tab through the real window, without showing desktop UI.
                    var main = new DBDOverlay.UI.Windows.MainWindow();
                    Assert.Equal(820, main.Width);
                    Assert.NotNull(main.FindName("ViewContent"));
                    Assert.False(DBDOverlay.Core.WindowControllers.KillerOverlay.KillerOverlayController.Overlay.ShowActivated);
                    DBDOverlay.Core.Reshade.ReshadeManager.Instance.StopReloadTimer();
                }
                catch (Exception error) { failure = error; }
            });
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "UI construction did not finish.");
            if (failure != null) throw failure;
        }
    }
}
