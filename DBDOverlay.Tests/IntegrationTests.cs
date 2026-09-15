using System;
using System.Drawing;
using System.IO;
using System.Linq;
using DBDOverlay.Core.Extensions;
using DBDOverlay.Core.Reshade;
using Xunit;

namespace DBDOverlay.Tests
{
    public class IntegrationTests
    {
        [Fact]
        public void KeyUpIsAlwaysSentAfterHoldIncludingExceptions()
        {
            var events = new System.Collections.Generic.List<string>();
            Assert.Equal(ReloadResult.Sent, ReloadKeyStroke.Pulse(down => { events.Add(down ? "down" : "up"); return true; }, () => events.Add("hold")));
            Assert.Equal(new[] { "down", "hold", "up" }, events);
            events.Clear();
            Assert.Throws<IOException>(() => ReloadKeyStroke.Pulse(down => { events.Add(down ? "down" : "up"); return true; }, () => throw new IOException()));
            Assert.Equal(new[] { "down", "up" }, events);
        }
        [Fact]
        public void LegacyMappingsIgnoreCorruptionAndKeepExactMapIndices()
        {
            var maps = Enumerable.Range(0, 15).Select(i => "MAP" + i).ToList();
            var result = PresetStore.Migrate("2-1,12-0,999-0,0-999,-1-0,garbage,3-x", maps, new[] { "A", "B" });
            Assert.Equal(2, result.Count); Assert.Equal("B", result["MAP2"]); Assert.Equal("A", result["MAP12"]);
            var restored = PresetStore.ReadMappings(PresetStore.WriteMappings(result));
            Assert.Equal("B", restored["map2"]);
        }
        [Fact]
        public void NamedMappingsSurviveFilterReorderingAndXmlCharacters()
        {
            var saved = PresetStore.Migrate("0-1", new[] { "MAP" }, new[] { "Z", "A & B" });
            var reopened = PresetStore.ReadMappings(PresetStore.WriteMappings(saved));
            var reordered = new[] { "A & B", "New filter", "Z" };
            Assert.Equal("A & B", reopened["MAP"]);
            Assert.Contains(reopened["MAP"], reordered);
            Assert.Empty(PresetStore.ReadMappings("broken xml"));
        }
        [Theory]
        [InlineData("")]
        [InlineData("..\\outside")]
        [InlineData("ReShade")]
        [InlineData("CON")]
        [InlineData("test.")]
        public void InvalidPresetNamesCannotTargetOtherFiles(string name)
            => Assert.Throws<ArgumentException>(() => PresetStore.Resolve(Path.GetTempPath(), name));

        [Fact]
        public void PresetCopyIsExactIdempotentAndPreservesDestinationOnFailure()
        {
            var folder = Path.Combine(Path.GetTempPath(), "DBDOverlay-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, "Source.ini"), "Techniques=Example\n[Effect.fx]\nValue=2");
                Assert.True(PresetStore.Copy(folder, "Source", "Output"));
                var original = File.ReadAllBytes(Path.Combine(folder, "Output.ini"));
                Assert.False(PresetStore.Copy(folder, "Source", "Output"));
                Assert.Throws<FileNotFoundException>(() => PresetStore.Copy(folder, "Missing", "Output"));
                Assert.Throws<IOException>(() => PresetStore.Copy(folder, "Source", "Source"));
                Assert.Equal(original, File.ReadAllBytes(Path.Combine(folder, "Output.ini")));
                Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
            }
            finally { Directory.Delete(folder, true); }
        }
        [Fact]
        public void ReloadParsesSectionsLfAndRelativePresetPath()
        {
            var folder = Path.GetTempPath(); var preset = Path.Combine(folder, "overlay.ini");
            var config = ReloadConfiguration.Parse("[OTHER]\nKeyReload=0\n[INPUT]\nKeyReload = 121,0,0,0\n[GENERAL]\nPresetPath=./overlay.ini", Path.Combine(folder, "ReShade.ini"), preset);
            Assert.Equal(121, config.Key); Assert.Equal(preset, config.PresetPath);
        }
        [Theory]
        [InlineData("0,0,0,0")]
        [InlineData("121,1,0,0")]
        [InlineData("65,0,0,0")]
        [InlineData("121")]
        [InlineData("bad")]
        [InlineData("121,0,0,0,1")]
        public void UnsupportedShortcutsAreRejected(string key)
            => Assert.Throws<InvalidOperationException>(() => ReloadConfiguration.Parse("[INPUT]\nKeyReload=" + key, "ReShade.ini", "overlay.ini"));
        [Fact]
        public void WrongPresetNeverTriggersReload()
            => Assert.Throws<InvalidOperationException>(() => ReloadConfiguration.Parse("[INPUT]\nKeyReload=121,0,0,0\n[GENERAL]\nPresetPath=other.ini", "ReShade.ini", "overlay.ini"));
        [Fact]
        public void ReloadWaitsThenSendsOnlyOnceAndLatestRequestWins()
        {
            var time = DateTime.UtcNow; var queue = new ReloadRequest(); int calls = 0;
            queue.Schedule("old", time); queue.Schedule("latest", time);
            Assert.Equal(ReloadResult.Waiting, queue.Tick(time, p => ReloadResult.Waiting));
            Assert.Equal(ReloadResult.Sent, queue.Tick(time, p => { Assert.Equal("latest", p); calls++; return ReloadResult.Sent; }));
            queue.Tick(time, p => { calls++; return ReloadResult.Sent; });
            Assert.Equal(1, calls); Assert.False(queue.Pending);
        }
        [Fact]
        public void DisableAndTimeoutPreventStaleReloads()
        {
            var now = DateTime.UtcNow; var queue = new ReloadRequest(); int calls = 0;
            Func<string, ReloadResult> send = p => { calls++; return ReloadResult.Sent; };
            queue.Schedule("preset", now); queue.Cancel(); queue.Tick(now, send);
            queue.Schedule("preset", now); Assert.Equal(ReloadResult.Failed, queue.Tick(now.AddSeconds(30), send));
            Assert.Equal(0, calls); Assert.False(queue.Pending);
        }
        [Fact]
        public void OcrThresholdAttemptsDoNotDestroyOriginalFrame()
        {
            using (var source = new Bitmap(8, 8))
            {
                using (var g = Graphics.FromImage(source)) g.Clear(Color.FromArgb(255, 100, 100, 100));
                using (var first = source.PreProcess(threshold: 400)) Assert.Equal(0, first.GetPixel(0, 0).R);
                using (var second = source.PreProcess(threshold: 200)) Assert.Equal(255, second.GetPixel(0, 0).R);
                Assert.Equal(100, source.GetPixel(0, 0).R);
            }
        }
    }
}
