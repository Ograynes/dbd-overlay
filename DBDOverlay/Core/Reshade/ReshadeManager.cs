using DBDOverlay.Core.Utils;
using DBDOverlay.Core.WindowControllers.MapOverlay;
using DBDOverlay.Core.WindowControllers.MapOverlay.Languages;
using DBDOverlay.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;

namespace DBDOverlay.Core.Reshade
{
    public class ReshadeManager
    {
        private static readonly Lazy<ReshadeManager> instance = new Lazy<ReshadeManager>(() => new ReshadeManager());
        public static ReshadeManager Instance => instance.Value;
        public List<string> Filters { get; private set; } = new List<string>();
        private Dictionary<string, string> mapFilterPairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly ReloadRequest reload = new ReloadRequest();
        private DispatcherTimer timer;
        public event EventHandler StatusChanged;
        public string Status { get; private set; } = "Choose a preset folder to get started.";
        public string LastFilter { get; private set; } = "None";
        public string LastMap { get; private set; } = "Waiting for a map";

        public void Initialize()
        {
            var path = Settings.Default.ReshadeFiltersPath;
            var original = string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) ? new List<string>() : FileSystem.GetIniFiles(path);
            var preferences = IntegrationSettings.Default;
            if (string.IsNullOrEmpty(preferences.NamedMappings) && original.Count > 0)
            {
                // Preserve the legacy enumeration order during this one-time migration.
                mapFilterPairs = PresetStore.Migrate(Settings.Default.ReshadeMappings, MapNamesContainer.GetReshadeMapsList(), original);
                preferences.MappingFolder = path;
                SaveMappings();
            }
            else if (string.Equals(preferences.MappingFolder, path, StringComparison.OrdinalIgnoreCase))
                mapFilterPairs = PresetStore.ReadMappings(preferences.NamedMappings);
            else mapFilterPairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Filters = original.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            SetStatus(Filters.Count == 0 ? "No filters found. Select a folder containing preset .ini files." : $"{Filters.Count} filters ready · {mapFilterPairs.Count} saved associations");
        }
        public void SetMapFilterPairs() => Initialize();
        public string GetFilterForMap(string map) => mapFilterPairs.TryGetValue(map, out var value) ? value : null;
        public void ApplyFilter(MapInfo map)
        {
            if (map == null) return;
            LastMap = map.FullName.Replace('_', ' ');
            var match = mapFilterPairs.Where(p => p.Value != null && (map.FullName.Equals(p.Key, StringComparison.OrdinalIgnoreCase) ||
                map.FullName.StartsWith(p.Key + ".", StringComparison.OrdinalIgnoreCase) ||
                map.FullName.Split('.').Any(part => part.Equals(p.Key, StringComparison.OrdinalIgnoreCase) || part.StartsWith(p.Key + "_", StringComparison.OrdinalIgnoreCase))))
                .OrderByDescending(p => p.Key.Length).FirstOrDefault();
            if (match.Value == null) { SetStatus("No filter assigned to this map. Assign one in ReShade integration."); return; }
            CopyFilter(match.Value, Settings.Default.MainFilterName);
        }
        public void ApplyBaseFilter()
        {
            if (Filters.Count == 0) { SetStatus("No source filter available."); return; }
            CopyFilter(Filters[0], Settings.Default.MainFilterName);
        }
        public void CopyFilter(string source, string destination)
        {
            try
            {
                PresetStore.Copy(Settings.Default.ReshadeFiltersPath, source, destination);
                LastFilter = source;
                if (IntegrationSettings.Default.AutoReload) RequestReload();
                else SetStatus($"{source} prepared. Press your ReShade reload key (usually F10).");
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            { reload.Cancel(); SetStatus("Filter not updated: " + error.Message); Logger.Warn(Status); }
        }
        public void AddFilterMapPair(string map, string filter)
        {
            if (string.IsNullOrEmpty(filter)) mapFilterPairs.Remove(map);
            else if (Filters.Contains(filter)) mapFilterPairs[map] = filter;
            SaveMappings();
        }
        public void AddFilterMapPair(string map, int index)
        { if (index >= 0 && index < Filters.Count) AddFilterMapPair(map, Filters[index]); }
        private void SaveMappings()
        {
            IntegrationSettings.Default.NamedMappings = PresetStore.WriteMappings(mapFilterPairs);
            IntegrationSettings.Default.MappingFolder = Settings.Default.ReshadeFiltersPath;
            IntegrationSettings.Default.Save();
        }
        public void ClearMapFilterPairs() { mapFilterPairs.Clear(); SaveMappings(); }
        public bool FilterExists(string name)
        { try { return File.Exists(PresetStore.Resolve(Settings.Default.ReshadeFiltersPath, name)); } catch (ArgumentException) { return false; } }
        public void StartReloadTimer()
        {
            if (timer != null) return;
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (s, e) => TickReload(); timer.Start();
        }
        public void StopReloadTimer() { timer?.Stop(); timer = null; reload.Cancel(); }
        public void CancelReload() { reload.Cancel(); SetStatus("Pending reload cleared. Your manual reload key still works."); }
        public void SetAutomaticReload(bool enabled)
        {
            reload.Cancel();
            IntegrationSettings.Default.AutoReload = enabled;
            IntegrationSettings.Default.Save();
            SetStatus(enabled ? "Automatic reload enabled for the next detected map. Keep the overlay preset selected in ReShade."
                : "Automatic reload off. Your manual reload key still works.");
        }
        public void RequestReload()
        {
            try
            {
                var preset = PresetStore.Resolve(Settings.Default.ReshadeFiltersPath, Settings.Default.MainFilterName);
                if (!File.Exists(preset)) throw new IOException("Create an overlay preset first.");
                var config = IntegrationSettings.Default.ConfigPath;
                if (!File.Exists(config)) throw new IOException("Choose the game's ReShade.ini first.");
                ReloadConfiguration.Parse(File.ReadAllText(config), config, preset);
                reload.Schedule(preset, DateTime.UtcNow);
                SetStatus("Preset ready. Waiting for DBD in the foreground (30 seconds).");
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is InvalidOperationException)
            { reload.Cancel(); SetStatus(error.Message); }
        }
        private void TickReload()
        {
            if (!reload.Pending) return;
            try
            {
                var result = reload.Tick(DateTime.UtcNow, preset => ReShadeReload.TrySend(IntegrationSettings.Default.ConfigPath, preset));
                if (result == ReloadResult.Sent) SetStatus($"Reload key sent · {LastFilter}. Check the effect in game.");
                else if (result == ReloadResult.Failed) SetStatus("Reload not sent. Return to DBD and use your manual reload key.");
            }
            catch (Exception error)
            { reload.Cancel(); SetStatus("Reload unavailable: " + error.Message); Logger.Warn(Status); }
        }
        private void SetStatus(string value) { Status = value; StatusChanged?.Invoke(this, EventArgs.Empty); }
    }
}
