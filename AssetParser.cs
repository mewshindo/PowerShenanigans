using System;
using System.IO;

namespace Wired
{
    public class AssetParser
    {
        private readonly string _path;

        public AssetParser(string path)
        {
            _path = path;
        }

        public bool HasEntry(string entry)
        {
            if (!File.Exists(_path))
                return false;

            using (var reader = File.OpenText(_path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith(entry, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        public bool TryGetFloat(string entry, out float value)
        {
            value = 0f;
            if (!File.Exists(_path))
                return false;

            using (var reader = File.OpenText(_path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith(entry, StringComparison.OrdinalIgnoreCase))
                    {
                        value = float.TryParse(line.Split(' ')[1], out float result) ? result : 0;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
