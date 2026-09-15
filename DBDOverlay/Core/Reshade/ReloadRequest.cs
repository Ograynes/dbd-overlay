using System;
using System.IO;
using System.Linq;

namespace DBDOverlay.Core.Reshade
{
    public enum ReloadResult { Waiting, Sent, Failed }

    public static class ReloadKeyStroke
    {
        public static ReloadResult Pulse(Func<bool, bool> send, Action hold)
        {
            if (!send(true)) return ReloadResult.Failed;
            bool released = false;
            try { hold(); }
            finally { released = send(false); }
            return released ? ReloadResult.Sent : ReloadResult.Failed;
        }
    }

    // UI-thread-owned queue: one latest request, no stale retries after disable/timeout.
    public sealed class ReloadRequest
    {
        private DateTime expires;
        public string Preset { get; private set; }
        public bool Pending => Preset != null;
        public void Schedule(string preset, DateTime now) { Preset = preset; expires = now.AddSeconds(30); }
        public void Cancel() => Preset = null;
        public ReloadResult Tick(DateTime now, Func<string, ReloadResult> send)
        {
            if (!Pending) return ReloadResult.Waiting;
            if (now >= expires) { Cancel(); return ReloadResult.Failed; }
            var result = send(Preset);
            if (result != ReloadResult.Waiting) Cancel();
            return result;
        }
    }

    public sealed class ReloadConfiguration
    {
        public ushort Key { get; private set; }
        public string PresetPath { get; private set; }
        public static string Value(string text, string section, string key)
        {
            string current = ""; string result = "";
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]")) { current = line.Substring(1, line.Length - 2); continue; }
                int equal = line.IndexOf('=');
                if (equal > 0 && current.Equals(section, StringComparison.OrdinalIgnoreCase) && line.Substring(0, equal).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    result = line.Substring(equal + 1).Trim().Trim('"');
            }
            return result;
        }
        public static ReloadConfiguration Parse(string text, string configPath, string expectedPreset)
        {
            var parts = Value(text, "INPUT", "KeyReload").Split(',');
            if (parts.Length != 4 || !ushort.TryParse(parts[0], out var key) || key < 112 || key > 135 || parts.Skip(1).Any(x => x.Trim() != "0"))
                throw new InvalidOperationException("Set the ReShade effect reload key to F1–F24 without modifiers (for example F10).");
            var preset = Value(text, "GENERAL", "PresetPath");
            if (string.IsNullOrWhiteSpace(preset)) throw new InvalidOperationException("Select the overlay preset in ReShade first.");
            var full = Path.GetFullPath(Path.IsPathRooted(preset) ? preset : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configPath)), preset));
            if (!full.Equals(Path.GetFullPath(expectedPreset), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ReShade has a different preset selected. Select the overlay preset first.");
            return new ReloadConfiguration { Key = key, PresetPath = full };
        }
    }
}
