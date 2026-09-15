using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;

namespace DBDOverlay.Core.ImageProcessing
{
    public sealed class DetectionSettings : ApplicationSettingsBase
    {
        public static DetectionSettings Default { get; } = (DetectionSettings)Synchronized(new DetectionSettings());
        [UserScopedSetting, DefaultSettingValue("")]
        public string FourRegion { get => (string)this[nameof(FourRegion)]; set => this[nameof(FourRegion)] = value; }
        [UserScopedSetting, DefaultSettingValue("")]
        public string EightRegion { get => (string)this[nameof(EightRegion)]; set => this[nameof(EightRegion)] = value; }

        public HudGeometry Geometry(Size size, bool eight) =>
            HudGeometry.TryParse(eight ? EightRegion : FourRegion, out var geometry) ? geometry : HudGeometry.Default(size, eight);

        public void SetRegion(HudGeometry geometry, bool eight)
        {
            if (eight) EightRegion = geometry?.ToString() ?? "";
            else FourRegion = geometry?.ToString() ?? "";
            Save();
            PortraitReferences.Clear(eight);
        }
    }

    public static class PortraitReferences
    {
        private static readonly object gate = new object();
        private static readonly Dictionary<bool, List<Bitmap>> cache = new Dictionary<bool, List<Bitmap>>();
        private static string Folder(bool eight) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DBDOverlay", "Calibration", eight ? "eight" : "four");

        private static List<Bitmap> Load(bool eight)
        {
            if (!cache.TryGetValue(eight, out var images))
            {
                images = new List<Bitmap>();
                if (Directory.Exists(Folder(eight)))
                    foreach (var file in Directory.GetFiles(Folder(eight), "portrait-*.png").Take(32))
                        try { using (var b = new Bitmap(file)) images.Add(new Bitmap(b)); }
                        catch (ArgumentException) { }
                cache[eight] = images;
            }
            return images;
        }

        public static void Save(Bitmap image, bool eight)
        {
            if (SurvivorImageMatcher.MatchPortrait(image, image) < .99) throw new ArgumentException("Select a visible, unhooked survivor state; the selected image is empty.");
            var state = SurvivorDetector.Classify(image, eight, Properties.Settings.Default.HooksThreshold);
            if (state == WindowControllers.KillerOverlay.HudObservation.Hooked || state == WindowControllers.KillerOverlay.HudObservation.Terminal)
                throw new ArgumentException("This image matches a hooked or terminal state. Select an unhooked survivor.");
            lock (gate)
            {
                var images = Load(eight);
                if (images.Count >= 32) throw new InvalidOperationException("32 references saved. Reset calibration before learning new references.");
                Directory.CreateDirectory(Folder(eight));
                image.Save(Path.Combine(Folder(eight), "portrait-" + Guid.NewGuid().ToString("N") + ".png"));
                images.Add(new Bitmap(image));
            }
        }

        public static double Match(Bitmap image, bool eight)
        {
            lock (gate) { return Load(eight).Select(reference => SurvivorImageMatcher.MatchPortrait(image, reference)).DefaultIfEmpty(0).Max(); }
        }

        public static void Clear(bool eight)
        {
            lock (gate)
            {
                if (cache.TryGetValue(eight, out var images)) foreach (var image in images) image.Dispose();
                cache.Remove(eight);
                if (Directory.Exists(Folder(eight))) foreach (var file in Directory.GetFiles(Folder(eight), "portrait-*.png")) File.Delete(file);
            }
        }
    }
}
