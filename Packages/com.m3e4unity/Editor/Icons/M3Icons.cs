using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// Material Symbols names (e.g. "favorite", "arrow_back") to the character that draws them.
    /// TextMesh Pro does not apply the font's ligatures, so icons are written as their code point.
    /// </summary>
    public static class M3Icons
    {
        const string CodepointsPath = "Packages/com.m3e4unity/Editor/Icons/MaterialSymbols.codepoints.txt";
        static Dictionary<string, string> map;

        static void Load()
        {
            if (map != null) return;
            map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(Path.GetFullPath(CodepointsPath)))
            {
                int sp = line.IndexOf(' ');
                if (sp <= 0) continue;
                int cp = int.Parse(line.Substring(sp + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                map[line.Substring(0, sp)] = char.ConvertFromUtf32(cp);
            }
        }

        public static IEnumerable<string> Names
        {
            get { Load(); return map.Keys; }
        }

        public static bool Exists(string name)
        {
            Load();
            return map.ContainsKey(name);
        }

        /// <summary>The string to put in a TMP text using the Material Symbols font.</summary>
        public static string Glyph(string name)
        {
            Load();
            if (map.TryGetValue(name, out var s)) return s;
            throw new ArgumentException($"Unknown Material Symbol '{name}'");
        }
    }
}
