using DBDOverlay.Core.Extensions;
using DBDOverlay.Core.Utils;
using DBDOverlay.Core.WindowControllers.KillerOverlay;
using DBDOverlay.Core.WindowControllers.MapOverlay;
using DBDOverlay.Core.WindowControllers.MapOverlay.Languages;
using DBDOverlay.Images.Maps;
using DBDOverlay.Images.SurvivorStates;
using DBDOverlay.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Application = System.Windows.Application;
using Tesseract;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Rectangle = System.Drawing.Rectangle;

namespace DBDOverlay.Core.ImageProcessing
{
    public class ImageReader
    {
        private int width;
        private int height;
        private readonly double maxScaleManual = 3;
        private readonly double maxScaleAuto = 1.0;
        private readonly double scaleStepManual = 1;
        private readonly double scaleStepAuto = 0.5;

        private readonly int maxThresholdForAuto = 750;
        private readonly int maxThresholdForManual = 400;
        private readonly int minThreshold = 300;
        private readonly int thresholdStep = 50;

        private readonly long operationTime = 200L;
        private string text = string.Empty;
        private int hooksThreshold;

        private TesseractEngine engine;
        private readonly object ocrGate = new object();
        private static ImageReader instance;

        public static ImageReader Instance
        {
            get
            {
                if (instance == null) instance = new ImageReader();
                return instance;
            }
        }

        public void Initialize()
        {
            SetScreenBounds();
            SetEngine();
            SetHooksThreshold(Settings.Default.HooksThreshold);
        }

        public void SetEngine()
        {
            lock (ocrGate)
            {
                var replacement = new TesseractEngine(FileSystem.TessData, LanguagesManager.ConvertMexToSpa(Settings.Default.Language));
                engine?.Dispose();
                engine = replacement;
            }
        }

        public MapInfo GetMapInfo(bool autoMode = false)
        {
            if (!GameCapture.TryGetBounds(out _)) { Thread.Sleep(200); return null; }
            if (!Monitor.TryEnter(ocrGate)) { Thread.Sleep(200); return null; }
            try { return GetMapInfoCore(autoMode); }
            finally { Monitor.Exit(ocrGate); }
        }

        private MapInfo GetMapInfoCore(bool autoMode)
        {
            var log = !autoMode;
            if (log) Logger.Info($"=============== Start getting map info ===============");
            var watch = Stopwatch.StartNew();
            using var bitmap = CreateFromScreenArea(autoMode ? RectType.Auto : RectType.Manual, log);

            var maxScale = autoMode ? maxScaleAuto : maxScaleManual;
            var scaleStep = autoMode ? scaleStepAuto : scaleStepManual;
            for (double scale = 1; scale <= maxScale; scale += scaleStep)
            {
                for (int threshold = autoMode ? maxThresholdForAuto : maxThresholdForManual; threshold >= minThreshold; threshold -= thresholdStep)
                {
                    if (log) Logger.Info($"===== Size = {scale}, Threshold = {threshold} =====");
                    var saveName = autoMode ? null : Settings.Default.ManualScreenshotFileName;
                    using (var preprocessed = bitmap.PreProcess(scale, threshold, saveName, true)) RecognizeText(preprocessed, log);
                    if (IsMapTextCorrect(autoMode))
                    {
                        if (log) Logger.Info("Map text is correct");
                        var mapInfo = ConvertTextToMapInfo(autoMode, log);
                        if (mapInfo.HasImage)
                        {
                            if (log) Logger.Info("Map has image file");
                            watch.Stop();
                            var time = watch.ElapsedMilliseconds;
                            mapInfo.Scale = scale;
                            mapInfo.Threshold = threshold;
                            mapInfo.Time = time;
                            if (log) Logger.Info($"=============== Finish getting map info ===============");
                            if (log) Logger.Info($"=============== ({time} ms) ===============");

                            return mapInfo;
                        }
                        if (scale == maxScale)
                        {
                            if (log) Logger.Warn($"Map file for '{mapInfo.FullName}' doesn't exist");
                        }
                    }
                    else
                    {
                        if (log) Logger.Warn("Incorrect map text:");
                        if (log) Logger.Warn(text);
                    }
                }
            }
            watch.Stop();
            if (log) Logger.Info($"=============== Finish getting map info ===============");
            if (log) Logger.Info($"=============== ({watch.ElapsedMilliseconds} ms) ===============");


            var delay = (int)(operationTime - watch.ElapsedMilliseconds);
            if (delay > 0) Thread.Sleep(delay);
            return null;
        }

        public void HandleSurvivors(bool is2v8Mode = false, bool saveImages = false)
        {
            int generation = KillerOverlayController.Instance.DetectionGeneration;
            if (!GameCapture.TryGetBounds(out var client))
            {
                Application.Current.Dispatcher.Invoke(() => KillerOverlayController.Instance.SuspendDetection());
                Thread.Sleep(200);
                return;
            }
            int count = is2v8Mode ? 8 : 4;
            var cells = DetectionSettings.Default.Geometry(client.Size, is2v8Mode).Cells(client, count);
            var observations = new HudObservation[count];
            // Take one coherent frame, not four/eight screenshots from different instants.
            var area = Rectangle.Union(cells[0], cells[count - 1]);
            using (var frame = GameCapture.Capture(area))
            {
                for (int i = 0; i < count; i++)
                {
                    var cell = cells[i]; cell.Offset(-area.X, -area.Y);
                    using (var piece = frame.Clone(cell, PixelFormat.Format32bppArgb))
                    {
                        observations[i] = SurvivorDetector.Classify(piece, is2v8Mode, hooksThreshold, () => PortraitReferences.Match(piece, is2v8Mode));
                        if (saveImages) piece.Save(FileSystem.GetImagePath($"survivor_{i}"), ImageFormat.Png);
                    }
                }
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!DBDOverlay.Core.BackgroundProcesses.KillerMode.Instance.IsActive || Settings.Default.Is2v8Mode != is2v8Mode ||
                    generation != KillerOverlayController.Instance.DetectionGeneration) return;
                for (int i = 0; i < count; i++) KillerOverlayController.Instance.Observe(i, observations[i]);
            });
            Thread.Sleep(200);
        }
        public Rectangle GetRect(RectType rectType, int w = 0, int h = 0)
        {
            return GetRect(GetRectMultiplier(rectType), w, h);
        }

        public Rectangle GetRect(RectMultiplier rectMultiplier, int w = 0, int h = 0)
        {
            if (w == 0) w = width;
            if (h == 0) h = height;
            var imageWidth = (w * rectMultiplier.Width).Round();
            var imageHeight = (h * rectMultiplier.Height).Round();
            return new Rectangle((w * rectMultiplier.X).Round(), (h * rectMultiplier.Y).Round(), imageWidth, imageHeight);
        }

        public void SetHooksThreshold(int value)
        {
            hooksThreshold = value;
        }

        private void SetScreenBounds()
        {
            var bounds = Screen.PrimaryScreen?.Bounds;
            if (bounds != null)
            {
                width = bounds.Value.Width;
                Logger.Info($"Screen width = {width}");
                height = bounds.Value.Height;
                Logger.Info($"Screen height = {height}");
            }
            else
            {
                Logger.Error("Screen bounds was not found");
            }
        }

        private Bitmap CreateFromScreenArea(RectType rectType, bool save = true)
        {
            if (!GameCapture.TryGetBounds(out var client)) throw new InvalidOperationException("Bring Dead by Daylight to the foreground to read the map.");
            var rect = GetRect(rectType, client.Width, client.Height);
            rect.Offset(client.Location);
            var bitmap = GameCapture.Capture(rect);

            if (save)
            {
                var path = FileSystem.GetImagePath();
                bitmap.Save(path, ImageFormat.Png);
                Logger.Info($"Image is saved to '{path}'");
                Logger.Info($"Area coordinates: x={rect.Left}, y={rect.Top}");
            }
            return bitmap;
        }

        private RectMultiplier GetRectMultiplier(RectType rectType)
        {
            switch (rectType)
            {
                case RectType.Manual: return new RectMultiplier(0.34, 0.8, 0.32, 0.08);
                case RectType.Auto: return new RectMultiplier(0.04, 0.81, 0.56, 0.05);
                case RectType.Gear: return new RectMultiplier(0.0472, 0.918, 0.0228, 0.0394);
                case RectType.Survivors: return new RectMultiplier(0.04, 0.38, 0.055, 0.326);
                case RectType.Survivors2v8: return new RectMultiplier(0.048, 0.186, 0.03, 0.524);
                case RectType.State: return new RectMultiplier(0.32, 0.25, 0.31, 0.5);
                case RectType.State2v8: return new RectMultiplier(0.134, 0.236, 0.64, 0.522);
                default: return null;
            }
        }

        private void RecognizeText(Bitmap bitmap, bool log = true)
        {
            var watch = Stopwatch.StartNew();
            using (var pixImage = PixConverter.ToPix(bitmap)) SetText(pixImage);
            watch.Stop();
            if (log) Logger.Info($"Text from image is recognized ({watch.ElapsedMilliseconds} ms)");
        }

        private void SetText(Pix pixImage)
        {
            Page page;
            try
            {
                page = engine.Process(pixImage);
            }
            catch (InvalidOperationException)
            {
                SetEngine();
                page = engine.Process(pixImage);
            }
            using (page) text = page.GetText();
        }

        private bool IsMapTextCorrect(bool autoMode = false)
        {
            return autoMode
                ? text.Length > 5 && text.ContainsRegex(@"\w")
                : text.Length > 5 && text.Contains('\n') && text.ContainsRegex(" - ");
        }

        private MapInfo ConvertTextToMapInfo(bool autoMode = false, bool log = true)
        {
            var mapInfo = autoMode ? ConvertStartTextToMapInfo() : ConvertEscTextToMapInfo();
            if (log) Logger.Info($"Map info: realm = {mapInfo.Realm}, name = {mapInfo.Name}");
            return mapInfo;
        }

        private MapInfo ConvertEscTextToMapInfo()
        {
            var res = text.Split(" - ");
            var realm = res[0].RemoveRegex("'").Replace(" ", "_").ToUpper();
            var mapName = res[1].Split('\n')[0].RemoveRegex("'").Replace(" ", "_").ToUpper();
            return new MapInfo(realm, HandleBadhamIssues(mapName));
        }

        private MapInfo ConvertStartTextToMapInfo()
        {
            var mapName = text.RemoveRegex(@"\n|'|\.").Replace(" ", "_").RemoveRegex(@"^_{1,}").ToUpper();
            return new MapInfo(HandleBadhamIssues(mapName, false), true);
        }

        private string HandleBadhamIssues(string mapName, bool log = true)
        {
            var tessWicknessPattern = @"(I|L|\||1|!)";
            var badhamSuffixPattern = $"_{tessWicknessPattern}{{1,3}}$";

            if (mapName.ContainsRegex(badhamSuffixPattern))
            {
                var oldMapName = mapName;
                var suffix = Regex.Match(mapName, badhamSuffixPattern).Value.Replace("|", @"\|");
                mapName = $"{mapName.RemoveRegex(suffix)}{suffix.ReplaceRegex(tessWicknessPattern, "I")}".RemoveRegex(@"\\");

                if (log) Logger.Info("Specific symbols are replaced");
                if (log) Logger.Info($"Old map name: '{oldMapName}'");
                if (log) Logger.Info($"New map name: '{mapName}'");
            }
            mapName = ReplaceSymbol(mapName, "№", "IV", log);
            mapName = ReplaceSymbol(mapName, @"\/", "V", log);
            return mapName;
        }

        private string ReplaceSymbol(string mapName, string oldString, string newString, bool log = true)
        {
            if (!mapName.EndsWith(oldString)) return mapName;
            if (oldString.StartsWith(@"\")) oldString = $@"\{oldString}";
            mapName = mapName.ReplaceRegex(oldString, newString);
            if (log) Logger.Info($"'{oldString}' symbol is replaced with '{newString}'");
            return mapName;
        }
    }
}
