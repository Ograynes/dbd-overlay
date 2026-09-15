using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DBDOverlay.Core.Utils;

namespace DBDOverlay.Core.Reshade
{
    public static class PresetStore
    {
        public static Dictionary<string, string> ReadMappings(string value)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value)) return result;
            try
            {
                foreach (var item in XElement.Parse(value).Elements("map"))
                {
                    string map = (string)item.Attribute("name"), filter = (string)item.Attribute("filter");
                    if (!string.IsNullOrWhiteSpace(map) && FileSystem.IsValidFileName(filter)) result[map] = filter;
                }
            }
            catch (System.Xml.XmlException) { }
            return result;
        }

        public static string WriteMappings(IDictionary<string, string> mappings) =>
            new XElement("mappings", mappings.Where(p => p.Value != null).Select(p =>
                new XElement("map", new XAttribute("name", p.Key), new XAttribute("filter", p.Value)))).ToString(SaveOptions.DisableFormatting);

        public static Dictionary<string, string> Migrate(string legacy, IList<string> maps, IList<string> filters)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in (legacy ?? "").Split(','))
            {
                var pair = entry.Split('-');
                if (pair.Length == 2 && int.TryParse(pair[0], out int map) && int.TryParse(pair[1], out int filter) &&
                    map >= 0 && map < maps.Count && filter >= 0 && filter < filters.Count) result[maps[map]] = filters[filter];
            }
            return result;
        }

        public static string Resolve(string folder, string name)
        {
            if (string.IsNullOrWhiteSpace(folder) || !FileSystem.IsValidFileName(name) || name.Equals("ReShade", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Select a preset folder and a valid preset name (not ReShade).");
            return Path.Combine(Path.GetFullPath(folder), name + ".ini");
        }

        public static bool Copy(string folder, string from, string to)
        {
            var source = Resolve(folder, from); var destination = Resolve(folder, to);
            if (source.Equals(destination, StringComparison.OrdinalIgnoreCase)) throw new IOException("The overlay preset must have a different name from the source filter.");
            var bytes = File.ReadAllBytes(source);
            if (File.Exists(destination) && File.ReadAllBytes(destination).SequenceEqual(bytes)) return false;
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
                return true;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
