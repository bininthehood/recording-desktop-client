using System.IO;

namespace RecordClient.Helpers.Config
{
    public class IniFile
    {
        private readonly string _path;
        private readonly Dictionary<string, Dictionary<string, string>> _data;

        public IniFile(string path)
        {
            _path = path;
            _data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(_path))
                Load();
        }

        public void Load()
        {
            _data.Clear();
            string? currentSection = null;

            foreach (var line in File.ReadAllLines(_path))
            {
                var trimmed = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(";"))
                    continue;

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed[1..^1];
                    if (!_data.ContainsKey(currentSection))
                        _data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                else if (currentSection != null && trimmed.Contains('='))
                {
                    var parts = trimmed.Split('=', 2);
                    _data[currentSection][parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        public string Get(string section, string key)
        {
            if (_data.TryGetValue(section, out var sectionDict))
            {
                if (sectionDict.TryGetValue(key, out var value))
                {
                    if (value != null) return value;
                }
            }
            return "";
        }

        public void Set(string section, string key, string value)
        {
            if (!_data.ContainsKey(section))
                _data[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            _data[section][key] = value;
        }

        public void Save()
        {
            using var writer = new StreamWriter(_path);
            foreach (var section in _data)
            {
                writer.WriteLine($"[{section.Key}]");
                foreach (var kv in section.Value)
                    writer.WriteLine($"{kv.Key}={kv.Value}");
                writer.WriteLine();
            }
        }
    }
}
