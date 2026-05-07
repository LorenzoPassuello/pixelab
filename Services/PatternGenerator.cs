using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pixelab
{
    public class PatternGenerator
    {
        public enum CompressionLevel { Off = 1, Low = 2, Medium = 3, High = 5 }
        public enum ColorSpace { RGB, Lab }

        #region Data Classes

        public class BeadColor
        {
            public string ColorId { get; set; } = "";
            public string Name { get; set; } = "";
            public string Hex { get; set; } = "";
            public double L { get; set; }
            public double A { get; set; }
            public double BLab { get; set; }
            public bool Enabled { get; set; } = true;
            public bool Favorite { get; set; }
            // Derived from Hex at load time; not persisted to JSON
            public byte R { get; set; }
            public byte G { get; set; }
            public byte B { get; set; }
            public string Group { get; set; } = "";
        }

        public class ColorGroup
        {
            public string GroupId { get; set; } = "";
            public string Name { get; set; } = "";
            public bool Enabled { get; set; } = true;
            public List<BeadColor> Colors { get; set; } = new();
        }

        public class ColorsData
        {
            public List<ColorGroup> Groups { get; set; } = new();
        }

        public class PatternData
        {
            public string Path { get; set; } = "";
            public IReadOnlyList<string> GroupFilter { get; set; } = [];
            public List<PatternColor> Colors { get; set; } = new();
        }

        public class PatternColor
        {
            public string ColorId { get; set; } = "";
            public List<PixelCoord> Pixels { get; set; } = new();
        }

        public class PixelCoord
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        #endregion

        private readonly string _colorsPath;
        private ColorsData? _colorsData;

        private IEnumerable<BeadColor> AllColors =>
            _colorsData?.Groups.SelectMany(g => g.Colors) ?? Enumerable.Empty<BeadColor>();

        public PatternGenerator(string colorsPath)
        {
            _colorsPath = colorsPath;
        }

        public List<ColorGroup> GetGroups()
        {
            LoadColors();
            return _colorsData?.Groups ?? new List<ColorGroup>();
        }

        public Dictionary<string, BeadColor> GetColorLookup()
        {
            LoadColors();
            return AllColors.ToDictionary(c => c.ColorId);
        }

        public void ReloadColors()
        {
            _colorsData = null;
            LoadColors();
        }

        public int GetNextColorNumber(string groupId)
        {
            LoadColors();

            var group = _colorsData?.Groups.FirstOrDefault(g => g.GroupId == groupId);
            var usedNumbers = new HashSet<int>();
            if (group != null)
            {
                foreach (var colorId in group.Colors.Select(c => c.ColorId))
                {
                    var parts = colorId.Split('_');
                    if (parts.Length > 0 && int.TryParse(parts[^1], out int num))
                        usedNumbers.Add(num);
                }
            }

            int next = 0;
            while (usedNumbers.Contains(next)) next++;
            return next;
        }

        public void LoadColors() => LoadColors(false);

        public void LoadColors(bool forceReload)
        {
            if (_colorsData != null && !forceReload) return;

            if (!File.Exists(_colorsPath))
            {
                _colorsData = new ColorsData();
                return;
            }

            string json = File.ReadAllText(_colorsPath);
            using var doc = JsonDocument.Parse(json);
            _colorsData = new ColorsData();

            if (doc.RootElement.TryGetProperty("groups", out var groupsArray))
            {
                foreach (var gEl in groupsArray.EnumerateArray())
                {
                    var group = new ColorGroup
                    {
                        GroupId = gEl.GetProperty("group_id").GetString() ?? "",
                        Name    = gEl.GetProperty("name").GetString() ?? "",
                        Enabled = gEl.GetProperty("enabled").GetBoolean()
                    };

                    if (gEl.TryGetProperty("colors", out var colorsArray))
                    {
                        foreach (var cEl in colorsArray.EnumerateArray())
                        {
                            string hex = cEl.GetProperty("hex").GetString() ?? "#000000";
                            (byte r, byte g, byte b) = HexToRgb(hex);

                            var color = new BeadColor
                            {
                                ColorId  = cEl.GetProperty("color_id").GetString() ?? "",
                                Name     = cEl.GetProperty("name").GetString() ?? "",
                                Hex      = hex,
                                Enabled  = cEl.GetProperty("enabled").GetBoolean(),
                                Favorite = cEl.GetProperty("favorite").GetBoolean(),
                                R = r, G = g, B = b,
                                Group = group.GroupId
                            };

                            if (cEl.TryGetProperty("l", out var l))    color.L    = l.GetDouble();
                            if (cEl.TryGetProperty("a", out var a))    color.A    = a.GetDouble();
                            if (cEl.TryGetProperty("b", out var bEl))  color.BLab = bEl.GetDouble();

                            group.Colors.Add(color);
                        }
                    }

                    _colorsData.Groups.Add(group);
                }
            }
        }

        private void SaveColorsJson()
        {
            if (_colorsData == null) return;

            var groupsArray = _colorsData.Groups.Select(g => new Dictionary<string, object>
            {
                ["group_id"] = g.GroupId,
                ["name"]     = g.Name,
                ["enabled"]  = g.Enabled,
                ["colors"]   = g.Colors.Select(c => new Dictionary<string, object>
                {
                    ["color_id"] = c.ColorId,
                    ["name"]     = c.Name,
                    ["hex"]      = c.Hex,
                    ["l"]        = c.L,
                    ["a"]        = c.A,
                    ["b"]        = c.BLab,
                    ["enabled"]  = c.Enabled,
                    ["favorite"] = c.Favorite
                }).ToList<object>()
            }).ToList<object>();

            var root = new Dictionary<string, object> { ["groups"] = groupsArray };
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_colorsPath, JsonSerializer.Serialize(root, options));
        }

        public void SaveColors() => SaveColorsJson();

        public void AddCustomColor(string colorId, byte r, byte g, byte b, string? name = null, string group = "custom")
        {
            LoadColors();

            string hex = $"#{r:X2}{g:X2}{b:X2}";
            RgbToLab(r, g, b, out double l, out double a, out double bLab);

            var targetGroup = _colorsData!.Groups.FirstOrDefault(gr => gr.GroupId == group);
            if (targetGroup == null)
            {
                targetGroup = new ColorGroup
                {
                    GroupId = group,
                    Name    = group == "custom" ? "Custom Colors" : group,
                    Enabled = true
                };
                _colorsData.Groups.Add(targetGroup);
            }

            var existing = AllColors.FirstOrDefault(c => c.ColorId == colorId);
            if (existing != null)
            {
                existing.Name = name ?? colorId;
                existing.Hex  = hex;
                existing.R = r; existing.G = g; existing.B = b;
                existing.L    = Math.Round(l, 1);
                existing.A    = Math.Round(a, 1);
                existing.BLab = Math.Round(bLab, 1);
                existing.Group = group;
            }
            else
            {
                targetGroup.Colors.Add(new BeadColor
                {
                    ColorId  = colorId,
                    Name     = name ?? colorId,
                    Hex      = hex,
                    R = r, G = g, B = b,
                    L        = Math.Round(l, 1),
                    A        = Math.Round(a, 1),
                    BLab     = Math.Round(bLab, 1),
                    Group    = group,
                    Enabled  = true,
                    Favorite = false
                });
            }

            SaveColors();
        }

        /// <summary>
        /// Imports colors from a JSON file. Supports both old flat format and new grouped format.
        /// Returns (imported, updated, skipped).
        /// </summary>
        public (int imported, int updated, int skippedDuplicates) ImportColorsFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return (0, 0, 0);

            LoadColors(true);

            string json = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(json);

            var colorElements = FlattenColorElements(doc.RootElement).ToList();

            int imported = 0, updated = 0, skippedDuplicates = 0;
            var seenIds = new HashSet<string>();

            foreach (var c in colorElements)
            {
                if (!c.TryGetProperty("color_id", out var colorIdProp)) continue;
                string colorId = colorIdProp.GetString() ?? "";
                if (string.IsNullOrEmpty(colorId)) continue;

                if (seenIds.Contains(colorId)) { skippedDuplicates++; continue; }
                seenIds.Add(colorId);

                if (!TryExtractColorValues(c, colorId, out var values)) continue;
                var (name, groupId, hex, r, g, b) = values;

                RgbToLab(r, g, b, out double l, out double a, out double bLab);

                var targetGroup = _colorsData!.Groups.FirstOrDefault(gr => gr.GroupId == groupId);
                if (targetGroup == null)
                {
                    targetGroup = new ColorGroup
                    {
                        GroupId = groupId,
                        Name    = groupId == "custom" ? "Custom Colors" : groupId,
                        Enabled = true
                    };
                    _colorsData.Groups.Add(targetGroup);
                }

                var existing = AllColors.FirstOrDefault(ec => ec.ColorId == colorId);
                if (existing != null)
                {
                    existing.Name = name; existing.Hex = hex;
                    existing.R = r; existing.G = g; existing.B = b;
                    existing.L = Math.Round(l, 1); existing.A = Math.Round(a, 1); existing.BLab = Math.Round(bLab, 1);
                    existing.Group = groupId;
                    updated++;
                }
                else
                {
                    targetGroup.Colors.Add(new BeadColor
                    {
                        ColorId = colorId, Name = name, Hex = hex,
                        R = r, G = g, B = b,
                        L = Math.Round(l, 1), A = Math.Round(a, 1), BLab = Math.Round(bLab, 1),
                        Group = groupId, Enabled = true, Favorite = false
                    });
                    imported++;
                }
            }

            if (imported > 0 || updated > 0) SaveColors();
            return (imported, updated, skippedDuplicates);
        }

        /// <summary>
        /// Imports colors from a JSON file, overriding all group assignments with the specified group.
        /// Returns (imported, updated, skipped).
        /// </summary>
        public (int imported, int updated, int skippedDuplicates) ImportColorsFromJsonWithGroup(
            string jsonPath, string groupId, string groupName)
        {
            if (!File.Exists(jsonPath)) return (0, 0, 0);

            LoadColors(true);

            string json = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(json);

            var colorElements = FlattenColorElements(doc.RootElement).ToList();

            var targetGroup = _colorsData!.Groups.FirstOrDefault(g => g.GroupId == groupId);
            if (targetGroup == null)
            {
                targetGroup = new ColorGroup { GroupId = groupId, Name = groupName, Enabled = true };
                _colorsData.Groups.Add(targetGroup);
            }

            int imported = 0, updated = 0, skippedDuplicates = 0;
            var seenIds = new HashSet<string>();

            foreach (var c in colorElements)
            {
                if (!c.TryGetProperty("color_id", out var colorIdProp)) continue;
                string colorId = colorIdProp.GetString() ?? "";
                if (string.IsNullOrEmpty(colorId)) continue;

                if (seenIds.Contains(colorId)) { skippedDuplicates++; continue; }
                seenIds.Add(colorId);

                if (!TryExtractColorValues(c, colorId, out var values)) continue;
                var (name, _, hex, r, g, b) = values;

                RgbToLab(r, g, b, out double l, out double a, out double bLab);

                var existing = AllColors.FirstOrDefault(ec => ec.ColorId == colorId);
                if (existing != null)
                {
                    existing.Name = name; existing.Hex = hex;
                    existing.R = r; existing.G = g; existing.B = b;
                    existing.L = Math.Round(l, 1); existing.A = Math.Round(a, 1); existing.BLab = Math.Round(bLab, 1);
                    existing.Group = groupId;
                    updated++;
                }
                else
                {
                    targetGroup.Colors.Add(new BeadColor
                    {
                        ColorId = colorId, Name = name, Hex = hex,
                        R = r, G = g, B = b,
                        L = Math.Round(l, 1), A = Math.Round(a, 1), BLab = Math.Round(bLab, 1),
                        Group = groupId, Enabled = true, Favorite = false
                    });
                    imported++;
                }
            }

            if (imported > 0 || updated > 0) SaveColors();
            return (imported, updated, skippedDuplicates);
        }

        public bool DeleteColor(string colorId)
        {
            LoadColors();
            foreach (var group in _colorsData!.Groups)
            {
                var color = group.Colors.FirstOrDefault(c => c.ColorId == colorId);
                if (color != null)
                {
                    group.Colors.Remove(color);
                    SaveColors();
                    return true;
                }
            }
            return false;
        }

        public bool DeleteGroup(string groupId)
        {
            LoadColors();
            var group = _colorsData!.Groups.FirstOrDefault(g => g.GroupId == groupId);
            if (group != null)
            {
                _colorsData.Groups.Remove(group);
                SaveColors();
                return true;
            }
            return false;
        }

        public (PatternData pattern, BitmapSource image) GeneratePattern(
            BitmapSource source, string imagePath, int alphaThreshold, CompressionLevel compression,
            ColorSpace colorSpace = ColorSpace.RGB, IReadOnlyCollection<string>? groupFilter = null)
        {
            LoadColors(true);

            var enabledColors = _colorsData!.Groups
                .Where(g => g.Enabled && (groupFilter == null || groupFilter.Count == 0 || groupFilter.Contains(g.GroupId)))
                .SelectMany(g => g.Colors.Where(c => c.Enabled))
                .ToList();

            if (enabledColors.Count == 0)
                throw new InvalidOperationException("No enabled colors. Enable at least one color group.");

            var conv = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
            byte[] pixels = new byte[h * stride];
            conv.CopyPixels(pixels, stride, 0);

            var uniqueColors = new Dictionary<int, List<PixelCoord>>();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * stride + x * 4;
                    if (pixels[idx + 3] < alphaThreshold) continue;

                    int key = (pixels[idx + 2] << 16) | (pixels[idx + 1] << 8) | pixels[idx];
                    if (!uniqueColors.ContainsKey(key))
                        uniqueColors[key] = new List<PixelCoord>();
                    uniqueColors[key].Add(new PixelCoord { X = x, Y = y });
                }
            }

            int n = (int)compression;
            var usedIds = new HashSet<string>();
            var mapping = new Dictionary<int, string>();

            foreach (var kvp in uniqueColors)
            {
                byte r = (byte)((kvp.Key >> 16) & 0xFF);
                byte g = (byte)((kvp.Key >> 8) & 0xFF);
                byte b = (byte)(kvp.Key & 0xFF);

                List<(BeadColor c, double dist)> topN;

                if (colorSpace == ColorSpace.Lab)
                {
                    RgbToLab(r, g, b, out double l, out double a, out double bLab);
                    topN = enabledColors
                        .Select(c => (c, dist: LabDistance(l, a, bLab, c.L, c.A, c.BLab)))
                        .OrderBy(x => x.dist)
                        .Take(n)
                        .ToList();
                }
                else
                {
                    topN = enabledColors
                        .Select(c => (c, dist: RgbDistance(r, g, b, c.R, c.G, c.B)))
                        .OrderBy(x => x.dist)
                        .Take(n)
                        .ToList();
                }

                var match = topN.FirstOrDefault(x => usedIds.Contains(x.c.ColorId));
                if (match.c == null) match = topN.FirstOrDefault(x => x.c.Favorite);
                if (match.c == null) match = topN[0];

                mapping[kvp.Key] = match.c.ColorId;
                usedIds.Add(match.c.ColorId);
            }

            var colorDict = new Dictionary<string, PatternColor>();
            foreach (var kvp in uniqueColors)
            {
                string id = mapping[kvp.Key];
                if (!colorDict.ContainsKey(id))
                    colorDict[id] = new PatternColor { ColorId = id };
                colorDict[id].Pixels.AddRange(kvp.Value);
            }

            var pattern = new PatternData
            {
                Path = imagePath, GroupFilter = groupFilter?.ToList() ?? [],
                Colors = colorDict.Values.ToList()
            };

            byte[] outPixels = new byte[h * stride];
            Array.Copy(pixels, outPixels, pixels.Length);

            var colorLookup = AllColors.ToDictionary(c => c.ColorId);
            foreach (var pc in pattern.Colors)
            {
                if (!colorLookup.TryGetValue(pc.ColorId, out var bc)) continue;
                foreach (var px in pc.Pixels)
                {
                    int idx = px.Y * stride + px.X * 4;
                    outPixels[idx]     = bc.B;
                    outPixels[idx + 1] = bc.G;
                    outPixels[idx + 2] = bc.R;
                }
            }

            var img = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, outPixels, stride);
            img.Freeze();

            return (pattern, img);
        }

        public BitmapSource GenerateImageFromPattern(PatternData pattern, BitmapSource source)
        {
            LoadColors();

            var conv = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
            byte[] pixels = new byte[h * stride];
            conv.CopyPixels(pixels, stride, 0);

            var colorLookup = AllColors.ToDictionary(c => c.ColorId);
            foreach (var pc in pattern.Colors)
            {
                if (!colorLookup.TryGetValue(pc.ColorId, out var bc)) continue;
                foreach (var px in pc.Pixels)
                {
                    int idx = px.Y * stride + px.X * 4;
                    if (idx + 3 < pixels.Length)
                    {
                        pixels[idx]     = bc.B;
                        pixels[idx + 1] = bc.G;
                        pixels[idx + 2] = bc.R;
                    }
                }
            }

            var img = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            img.Freeze();
            return img;
        }

        #region Color Conversion and Helpers

        private static (byte r, byte g, byte b) HexToRgb(string hex)
        {
            hex = hex.TrimStart('#');
            return (
                Convert.ToByte(hex[0..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16)
            );
        }

        // Returns a flat sequence of color JSON elements from either old (colors[]) or new (groups[].colors[]) format.
        private static IEnumerable<JsonElement> FlattenColorElements(JsonElement root)
        {
            if (root.TryGetProperty("colors", out var flat))
                return flat.EnumerateArray();
            if (root.TryGetProperty("groups", out var groups))
                return groups.EnumerateArray()
                    .SelectMany(g => g.TryGetProperty("colors", out var gc)
                        ? gc.EnumerateArray()
                        : Enumerable.Empty<JsonElement>());
            return Enumerable.Empty<JsonElement>();
        }

        private static bool TryExtractColorValues(
            JsonElement c, string fallbackName,
            out (string name, string groupId, string hex, byte r, byte g, byte b) values)
        {
            string name = c.TryGetProperty("name", out var np) ? np.GetString() ?? fallbackName : fallbackName;
            string groupId = c.TryGetProperty("group", out var gp) ? gp.GetString() ?? "custom" : "custom";

            if (c.TryGetProperty("hex", out var hexProp))
            {
                string hex = hexProp.GetString() ?? "#000000";
                (byte r, byte g, byte b) = HexToRgb(hex);
                values = (name, groupId, hex, r, g, b);
                return true;
            }
            if (c.TryGetProperty("r", out var rp) && c.TryGetProperty("g", out var gpp) && c.TryGetProperty("b", out var bp))
            {
                byte r = (byte)rp.GetInt32();
                byte g = (byte)gpp.GetInt32();
                byte b = (byte)bp.GetInt32();
                string hex = $"#{r:X2}{g:X2}{b:X2}";
                values = (name, groupId, hex, r, g, b);
                return true;
            }

            values = default;
            return false;
        }

        public static void RgbToLab(byte r, byte g, byte b, out double l, out double a, out double bLab)
        {
            double rr = r / 255.0, gg = g / 255.0, bb = b / 255.0;

            rr = rr > 0.04045 ? Math.Pow((rr + 0.055) / 1.055, 2.4) : rr / 12.92;
            gg = gg > 0.04045 ? Math.Pow((gg + 0.055) / 1.055, 2.4) : gg / 12.92;
            bb = bb > 0.04045 ? Math.Pow((bb + 0.055) / 1.055, 2.4) : bb / 12.92;

            double x = (rr * 0.4124564 + gg * 0.3575761 + bb * 0.1804375) / 0.95047;
            double y = (rr * 0.2126729 + gg * 0.7151522 + bb * 0.0721750);
            double z = (rr * 0.0193339 + gg * 0.1191920 + bb * 0.9503041) / 1.08883;

            x = x > 0.008856 ? Math.Pow(x, 1.0 / 3.0) : (7.787 * x) + (16.0 / 116.0);
            y = y > 0.008856 ? Math.Pow(y, 1.0 / 3.0) : (7.787 * y) + (16.0 / 116.0);
            z = z > 0.008856 ? Math.Pow(z, 1.0 / 3.0) : (7.787 * z) + (16.0 / 116.0);

            l    = (116.0 * y) - 16.0;
            a    = 500.0 * (x - y);
            bLab = 200.0 * (y - z);
        }

        public static double LabDistance(double l1, double a1, double b1, double l2, double a2, double b2)
        {
            double dl = l1 - l2, da = a1 - a2, db = b1 - b2;
            return Math.Sqrt(dl * dl + da * da + db * db);
        }

        public static double RgbDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            int dr = r1 - r2, dg = g1 - g2, db = b1 - b2;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        #endregion
    }
}
