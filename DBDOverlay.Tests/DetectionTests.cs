using System;
using System.Drawing;
using System.Globalization;
using DBDOverlay.Core.ImageProcessing;
using Xunit;

namespace DBDOverlay.Tests
{
    public class DetectionTests
    {
        [Theory]
        [InlineData(2, -1)]
        [InlineData(-2, 1)]
        public void SmallCalibrationOffsetStillRecognizesHook(int dx, int dy)
        {
            using (var reference = new Bitmap(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "hooked.png")))
            using (var shifted = new Bitmap(reference.Width, reference.Height))
            {
                using (var g = Graphics.FromImage(shifted))
                {
                    g.Clear(Color.Black);
                    g.DrawImageUnscaled(reference, dx, dy);
                }
                Assert.Equal(DBDOverlay.Core.WindowControllers.KillerOverlay.HudObservation.Hooked,
                    SurvivorDetector.Classify(shifted, false, 600));
            }
        }
        [Fact]
        public void UnhookRequiresLearnedAppearanceAndBlankStillStaysUnknown()
        {
            using (var portrait = new Bitmap(33, 44))
            using (var blank = new Bitmap(33, 44))
            {
                using (var g = Graphics.FromImage(portrait))
                {
                    g.Clear(Color.DarkBlue);
                    g.FillEllipse(Brushes.Sienna, 5, 5, 20, 30);
                }
                Assert.Equal(DBDOverlay.Core.WindowControllers.KillerOverlay.HudObservation.Unknown, SurvivorDetector.Classify(portrait, false, 600));
                Assert.Equal(DBDOverlay.Core.WindowControllers.KillerOverlay.HudObservation.Unhooked,
                    SurvivorDetector.Classify(portrait, false, 600, () => SurvivorImageMatcher.MatchPortrait(portrait, portrait)));
                Assert.Equal(DBDOverlay.Core.WindowControllers.KillerOverlay.HudObservation.Unknown, SurvivorDetector.Classify(blank, false, 600, () => 1));
            }
        }
        [Theory]
        [InlineData("hooked.png")]
        [InlineData("hooked2.png")]
        [InlineData("hooked3.png")]
        public void HookMatchesAt1440pCaptureSize(string file)
        {
            using (var source = new Bitmap(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", file)))
            using (var image = new Bitmap(source, 44, 58))
                Assert.Equal(DBDOverlay.Core.WindowControllers.KillerOverlay.HudObservation.Hooked, SurvivorDetector.Classify(image, false, 600));
        }
        [Theory]
        [InlineData("hooked.png", false)]
        [InlineData("hooked2.png", false)]
        [InlineData("hooked3.png", false)]
        [InlineData("2v8/hooked2v8_0.png", true)]
        [InlineData("2v8/hooked2v8_1.png", true)]
        [InlineData("2v8/hooked2v8_2.png", true)]
        [InlineData("2v8/hooked2v8_3.png", true)]
        [InlineData("2v8/hooked2v8_4.png", true)]
        public void ShippedHookTemplatesStillRecognizeAtDifferentSizes(string file, bool eight)
        {
            using (var source = new Bitmap(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", file)))
            using (var image = new Bitmap(source, source.Width * 2, source.Height * 2))
                Assert.Equal(DBDOverlay.Core.WindowControllers.KillerOverlay.HudObservation.Hooked, SurvivorDetector.Classify(image, eight, 600));
        }
        [Theory]
        [InlineData("dead.png", false)]
        [InlineData("escaped.png", false)]
        [InlineData("sacrificed.png", false)]
        [InlineData("sacrificed2.png", false)]
        [InlineData("2v8/escaped_2v8_0.png", true)]
        [InlineData("2v8/escaped_2v8_1.png", true)]
        [InlineData("2v8/escaped_2v8_2.png", true)]
        [InlineData("2v8/escaped_2v8_3.png", true)]
        [InlineData("2v8/sacrificed_2v8_0.png", true)]
        [InlineData("2v8/sacrificed_2v8_1.png", true)]
        [InlineData("2v8/sacrificed_2v8_2.png", true)]
        [InlineData("2v8/sacrificed_2v8_3.png", true)]
        public void TerminalTemplatesDoNotTriggerUnhook(string file, bool eight)
        {
            using (var source = new Bitmap(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", file)))
            using (var image = new Bitmap(source, source.Width * 2, source.Height * 2))
                Assert.Equal(DBDOverlay.Core.WindowControllers.KillerOverlay.HudObservation.Terminal, SurvivorDetector.Classify(image, eight, 600));
        }

        [Fact]
        public void SettingsPersistDistinctProfilesAfterReload()
        {
            // The test host has its own LocalFileSettingsProvider store, separate from DBDOverlay.exe.
            var settings = new DetectionSettings();
            var four = settings.FourRegion; var eight = settings.EightRegion;
            try
            {
                settings.FourRegion = "0.1,0.2,0.03,0.4";
                settings.EightRegion = "0.2,0.1,0.02,0.6";
                settings.Save();
                var restored = new DetectionSettings(); restored.Reload();
                Assert.Equal(settings.FourRegion, restored.FourRegion);
                Assert.Equal(settings.EightRegion, restored.EightRegion);
            }
            finally { settings.FourRegion = four; settings.EightRegion = eight; settings.Save(); }
        }
        [Theory]
        [InlineData(1920, 1080, 111)]
        [InlineData(2560, 1440, 147)]
        [InlineData(3440, 1440, 147)]
        public void HudUsesHeightInsteadOfUltrawideWidth(int width, int height, int left)
        {
            var geometry = HudGeometry.Default(new Size(width, height), false);
            var cells = geometry.Cells(new Rectangle(-1920, 250, width, height), 4);
            Assert.Equal(-1920 + left, cells[0].Left);
            Assert.All(cells, cell => Assert.True(cell.Width > 0 && cell.Height > 0));
            Assert.True(cells[3].Bottom <= 250 + height);
            Assert.InRange(Math.Abs((int)Math.Round(height * .0815) - (cells[1].Y - cells[0].Y)), 0, 1);
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(1.25)]
        [InlineData(1.5)]
        public void CalibrationScalesWithPhysicalClientPixels(double dpiScale)
        {
            var region = new HudGeometry(new RectangleF(.05f, .4f, .02f, .3f));
            var rect = new Rectangle(100, 200, (int)(1920 * dpiScale), (int)(1080 * dpiScale));
            var cells = region.Cells(rect, 4);
            Assert.InRange(Math.Abs(cells[0].X - (100 + 96 * dpiScale)), 0, 1);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(8)]
        public void LastCellAndGapAreIncluded(int count)
        {
            var region = new HudGeometry(new RectangleF(.1f, .2f, .04f, .5f));
            var cells = region.Cells(new Rectangle(0, 0, 1920, 1080), count);
            Assert.Equal(count, cells.Length);
            Assert.InRange(Math.Abs(cells[count - 1].Bottom - 756), 0, 1);
            for (int i = 1; i < count; i++) Assert.True(cells[i].Top >= cells[i - 1].Bottom);
        }

        [Fact]
        public void CalibrationRoundTripsAcrossCultures()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var region = new HudGeometry(new RectangleF(.1f, .2f, .03f, .4f));
                Assert.True(HudGeometry.TryParse(region.ToString(), out var restored));
                Assert.Equal(region.Region, restored.Region);
            }
            finally { CultureInfo.CurrentCulture = original; }
        }

        [Theory]
        [InlineData("")]
        [InlineData("NaN,0,1,1")]
        [InlineData("0,0,0,1")]
        [InlineData("-1,0,1,1")]
        [InlineData("0.9,0,0.3,1")]
        public void InvalidCalibrationIsRejected(string value) => Assert.False(HudGeometry.TryParse(value, out _));

        [Fact]
        public void ScaledNonSquareImageMatchesWithoutChangingSource()
        {
            using (var reference = Pattern())
            using (var larger = new Bitmap(reference, 66, 88))
            {
                Assert.True(SurvivorImageMatcher.Match(larger, reference, 400) > .9);
                Assert.Equal(new Size(66, 88), larger.Size);
            }
        }

        [Fact]
        public void BlankAndUnrelatedImagesCannotMatchBySharingBackground()
        {
            using (var reference = Pattern())
            using (var blank = new Bitmap(66, 88))
            using (var other = new Bitmap(33, 44))
            {
                using (var g = Graphics.FromImage(other)) g.FillRectangle(Brushes.White, 0, 25, 8, 10);
                Assert.Equal(0, SurvivorImageMatcher.Match(blank, reference));
                Assert.Equal(0, SurvivorImageMatcher.Match(blank, blank));
                Assert.True(SurvivorImageMatcher.Match(other, reference) < .4);
                Assert.Equal(0, SurvivorImageMatcher.MatchPortrait(blank, blank));
                Assert.Equal(0, SurvivorImageMatcher.MatchPortrait(blank, reference));
            }
        }

        private static Bitmap Pattern()
        {
            var b = new Bitmap(33, 44);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Color.Black);
                g.FillRectangle(Brushes.White, 14, 2, 5, 37);
                g.FillRectangle(Brushes.White, 8, 8, 18, 5);
            }
            return b;
        }
    }
}
