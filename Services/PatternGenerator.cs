using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pixelab
{
    /// <summary>
    /// Generates bead patterns from pixel art images by matching colors
    /// to available bead colors from the database.
    /// </summary>
    public class PatternGenerator
    {
        /// <summary>Compression level for color reduction during pattern generation.</summary>
        public enum CompressionLevel { Off = 1, Low = 2, Medium = 3, High = 5 }
        
        /// <summary>Color space used for calculating color distances.</summary>
        public enum ColorSpace { RGB, Lab }

        #region Data Classes

        public class BeadColor
        {
            public string ColorId { get; set; } = "";
            public string Name { get; set; } = "";
            public byte R { get; set; }
            public byte G { get; set; }
            public byte B { get; set; }
            public double L { get; set; }
            public double A_Lab { get; set; }
            public double B_Lab { get; set; }
            public string Group { get; set; } = "";
            public bool Enabled { get; set; } = true;
            public bool Favorite { get; set; }
        }

        public class ColorGroup
        {
            public string GroupId { get; set; } = "";
            public string Name { get; set; } = "";
            public bool Enabled { get; set; } = true;
        }

        public class ColorsData
        {
            public List<BeadColor> Colors { get; set; } = new();
            public List<ColorGroup> Groups { get; set; } = new();
        }

        public class PatternData
        {
            public string Path { get; set; } = "";
            public string GroupFilter { get; set; } = "all"; // "all" or specific group_id
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
        private readonly string _patternsPath;
        private ColorsData? _colorsData;

        public PatternGenerator(string colorsPath, string patternsPath)
        {
            _colorsPath = colorsPath;
            _patternsPath = patternsPath;
        }

        /// <summary>
        /// Gets the list of all color groups.
        /// </summary>
        public List<ColorGroup> GetGroups()
        {
            LoadColors();
            return _colorsData?.Groups ?? new List<ColorGroup>();
        }
        
        /// <summary>
        /// Reloads colors from file and refreshes the groups ComboBox.
        /// </summary>
        public void ReloadColors()
        {
            _colorsData = null;
            LoadColors();
        }
        
        /// <summary>
        /// Gets the next available color number for a group.
        /// Returns the first unused number starting from 0.
        /// </summary>
        public int GetNextColorNumber(string groupId)
        {
            LoadColors();
            
            var groupColors = _colorsData?.Colors
                .Where(c => c.Group == groupId)
                .Select(c => c.ColorId)
                .ToList() ?? new List<string>();
            
            // Find numbers already used
            var usedNumbers = new HashSet<int>();
            foreach (var colorId in groupColors)
            {
                // Try to extract number from color ID (format: PREFIX_NNN)
                var parts = colorId.Split('_');
                if (parts.Length > 0)
                {
                    var lastPart = parts[parts.Length - 1];
                    if (int.TryParse(lastPart, out int num))
                    {
                        usedNumbers.Add(num);
                    }
                }
            }
            
            // Find first unused number
            int next = 0;
            while (usedNumbers.Contains(next))
            {
                next++;
            }
            
            return next;
        }

        public void LoadColors()
        {
            LoadColors(false);
        }
        
        public void LoadColors(bool forceReload)
        {
            if (_colorsData != null && !forceReload) return; // Already loaded
            
            if (!File.Exists(_colorsPath))
            {
                _colorsData = new ColorsData();
                return;
            }

            string json = File.ReadAllText(_colorsPath);
            using var doc = JsonDocument.Parse(json);
            _colorsData = new ColorsData();

            if (doc.RootElement.TryGetProperty("colors", out var colorsArray))
            {
                foreach (var el in colorsArray.EnumerateArray())
                {
                    var color = new BeadColor
                    {
                        ColorId = el.GetProperty("color_id").GetString() ?? "",
                        Name = el.GetProperty("name").GetString() ?? "",
                        R = el.GetProperty("r").GetByte(),
                        G = el.GetProperty("g").GetByte(),
                        B = el.GetProperty("b").GetByte(),
                        Group = el.GetProperty("group").GetString() ?? "",
                        Enabled = el.GetProperty("enabled").GetBoolean(),
                        Favorite = el.GetProperty("favorite").GetBoolean()
                    };
                    
                    if (el.TryGetProperty("l", out var l)) color.L = l.GetDouble();
                    if (el.TryGetProperty("a_lab", out var a)) color.A_Lab = a.GetDouble();
                    if (el.TryGetProperty("b_lab", out var b)) color.B_Lab = b.GetDouble();
                    
                    _colorsData.Colors.Add(color);
                }
            }

            if (doc.RootElement.TryGetProperty("groups", out var groupsArray))
            {
                foreach (var el in groupsArray.EnumerateArray())
                {
                    _colorsData.Groups.Add(new ColorGroup
                    {
                        GroupId = el.GetProperty("group_id").GetString() ?? "",
                        Name = el.GetProperty("name").GetString() ?? "",
                        Enabled = el.GetProperty("enabled").GetBoolean()
                    });
                }
            }

            UpdateLabValuesIfNeeded();
        }

        private void UpdateLabValuesIfNeeded()
        {
            if (_colorsData == null) return;
            
            bool needsSave = false;
            foreach (var color in _colorsData.Colors)
            {
                if (color.L == 0 && color.A_Lab == 0 && color.B_Lab == 0 && 
                    !(color.R == 0 && color.G == 0 && color.B == 0))
                {
                    RgbToLab(color.R, color.G, color.B, out double l, out double a, out double b);
                    color.L = Math.Round(l, 1);
                    color.A_Lab = Math.Round(a, 1);
                    color.B_Lab = Math.Round(b, 1);
                    needsSave = true;
                }
            }
            
            if (needsSave) SaveColorsJson();
        }

        private void SaveColorsJson()
        {
            if (_colorsData == null) return;
            
            var colorsArray = _colorsData.Colors.Select(c => new Dictionary<string, object>
            {
                ["color_id"] = c.ColorId,
                ["name"] = c.Name,
                ["hex"] = $"#{c.R:X2}{c.G:X2}{c.B:X2}",
                ["r"] = c.R,
                ["g"] = c.G,
                ["b"] = c.B,
                ["l"] = c.L,
                ["a_lab"] = c.A_Lab,
                ["b_lab"] = c.B_Lab,
                ["group"] = c.Group,
                ["enabled"] = c.Enabled,
                ["favorite"] = c.Favorite
            }).ToList();
            
            var groupsArray = _colorsData.Groups.Select(g => new Dictionary<string, object>
            {
                ["group_id"] = g.GroupId,
                ["name"] = g.Name,
                ["enabled"] = g.Enabled
            }).ToList();
            
            var root = new Dictionary<string, object>
            {
                ["colors"] = colorsArray,
                ["groups"] = groupsArray
            };
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_colorsPath, JsonSerializer.Serialize(root, options));
        }
        
        /// <summary>
        /// Public method to save colors data.
        /// </summary>
        public void SaveColors() => SaveColorsJson();

        /// <summary>
        /// Adds a custom color to the colors database.
        /// </summary>
        public void AddCustomColor(string colorId, byte r, byte g, byte b, string? name = null, string group = "custom")
        {
            LoadColors();
            
            // Calculate hex
            string hex = $"#{r:X2}{g:X2}{b:X2}";
            
            // Calculate Lab values
            RgbToLab(r, g, b, out double l, out double a_lab, out double b_lab);
            
            // Create new color
            var newColor = new BeadColor
            {
                ColorId = colorId,
                Name = name ?? colorId,
                R = r,
                G = g,
                B = b,
                L = Math.Round(l, 1),
                A_Lab = Math.Round(a_lab, 1),
                B_Lab = Math.Round(b_lab, 1),
                Group = group,
                Enabled = true,
                Favorite = false
            };
            
            // Check if group exists, create if not
            if (!_colorsData!.Groups.Any(g => g.GroupId == group))
            {
                _colorsData.Groups.Add(new ColorGroup
                {
                    GroupId = group,
                    Name = group == "custom" ? "Custom Colors" : group,
                    Enabled = true
                });
            }
            
            // Check if color_id already exists
            var existing = _colorsData.Colors.FirstOrDefault(c => c.ColorId == colorId);
            if (existing != null)
            {
                // Update existing color
                existing.Name = newColor.Name;
                existing.R = newColor.R;
                existing.G = newColor.G;
                existing.B = newColor.B;
                existing.L = newColor.L;
                existing.A_Lab = newColor.A_Lab;
                existing.B_Lab = newColor.B_Lab;
                existing.Group = newColor.Group;
            }
            else
            {
                _colorsData.Colors.Add(newColor);
            }
            
            SaveColors();
        }

        /// <summary>
        /// Imports colors from a JSON file. Returns (imported, updated, skipped).
        /// </summary>
        public (int imported, int updated, int skippedDuplicates) ImportColorsFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return (0, 0, 0);
            
            LoadColors(true); // Force reload to get latest state
            
            string json = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(json);
            
            int imported = 0;
            int updated = 0;
            var seenIds = new HashSet<string>();
            int skippedDuplicates = 0;
            
            if (doc.RootElement.TryGetProperty("colors", out var colorsArr))
            {
                foreach (var c in colorsArr.EnumerateArray())
                {
                    // Required fields
                    if (!c.TryGetProperty("color_id", out var colorIdProp)) continue;
                    if (!c.TryGetProperty("r", out var rProp)) continue;
                    if (!c.TryGetProperty("g", out var gProp)) continue;
                    if (!c.TryGetProperty("b", out var bProp)) continue;
                    
                    string colorId = colorIdProp.GetString() ?? "";
                    if (string.IsNullOrEmpty(colorId)) continue;
                    
                    // Check for duplicates within the import file itself
                    if (seenIds.Contains(colorId))
                    {
                        skippedDuplicates++;
                        continue;
                    }
                    seenIds.Add(colorId);
                    
                    byte r = (byte)rProp.GetInt32();
                    byte g = (byte)gProp.GetInt32();
                    byte b = (byte)bProp.GetInt32();
                    
                    // Optional fields
                    string name = c.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? colorId : colorId;
                    string group = c.TryGetProperty("group", out var groupProp) ? groupProp.GetString() ?? "custom" : "custom";
                    
                    // Calculate hex and Lab
                    string hex = $"#{r:X2}{g:X2}{b:X2}";
                    RgbToLab(r, g, b, out double l, out double a_lab, out double b_lab);
                    
                    // Ensure group exists
                    if (!_colorsData!.Groups.Any(grp => grp.GroupId == group))
                    {
                        _colorsData.Groups.Add(new ColorGroup
                        {
                            GroupId = group,
                            Name = group == "custom" ? "Custom Colors" : group,
                            Enabled = true
                        });
                    }
                    
                    // Check if color exists in database
                    var existing = _colorsData.Colors.FirstOrDefault(ec => ec.ColorId == colorId);
                    if (existing != null)
                    {
                        existing.Name = name;
                        existing.R = r;
                        existing.G = g;
                        existing.B = b;
                        existing.L = Math.Round(l, 1);
                        existing.A_Lab = Math.Round(a_lab, 1);
                        existing.B_Lab = Math.Round(b_lab, 1);
                        existing.Group = group;
                        updated++;
                    }
                    else
                    {
                        _colorsData.Colors.Add(new BeadColor
                        {
                            ColorId = colorId,
                            Name = name,
                            R = r,
                            G = g,
                            B = b,
                            L = Math.Round(l, 1),
                            A_Lab = Math.Round(a_lab, 1),
                            B_Lab = Math.Round(b_lab, 1),
                            Group = group,
                            Enabled = true,
                            Favorite = false
                        });
                        imported++;
                    }
                }
            }
            
            if (imported > 0 || updated > 0)
                SaveColors();
            
            return (imported, updated, skippedDuplicates);
        }

        /// <summary>
        /// Imports colors from a JSON file, overriding all color group assignments with the specified group.
        /// Returns (imported, updated, skipped).
        /// </summary>
        public (int imported, int updated, int skippedDuplicates) ImportColorsFromJsonWithGroup(
            string jsonPath, string groupId, string groupName)
        {
            if (!File.Exists(jsonPath)) return (0, 0, 0);

            LoadColors(true);

            string json = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(json);

            int imported = 0;
            int updated = 0;
            var seenIds = new HashSet<string>();
            int skippedDuplicates = 0;

            // Ensure the target group exists
            if (!_colorsData!.Groups.Any(g => g.GroupId == groupId))
            {
                _colorsData.Groups.Add(new ColorGroup
                {
                    GroupId = groupId,
                    Name = groupName,
                    Enabled = true
                });
            }

            if (doc.RootElement.TryGetProperty("colors", out var colorsArr))
            {
                foreach (var c in colorsArr.EnumerateArray())
                {
                    if (!c.TryGetProperty("color_id", out var colorIdProp)) continue;
                    if (!c.TryGetProperty("r", out var rProp)) continue;
                    if (!c.TryGetProperty("g", out var gProp)) continue;
                    if (!c.TryGetProperty("b", out var bProp)) continue;

                    string colorId = colorIdProp.GetString() ?? "";
                    if (string.IsNullOrEmpty(colorId)) continue;

                    if (seenIds.Contains(colorId)) { skippedDuplicates++; continue; }
                    seenIds.Add(colorId);

                    byte r = (byte)rProp.GetInt32();
                    byte g = (byte)gProp.GetInt32();
                    byte b = (byte)bProp.GetInt32();
                    string name = c.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? colorId : colorId;

                    RgbToLab(r, g, b, out double l, out double a_lab, out double b_lab);

                    var existing = _colorsData.Colors.FirstOrDefault(ec => ec.ColorId == colorId);
                    if (existing != null)
                    {
                        existing.Name = name;
                        existing.R = r;
                        existing.G = g;
                        existing.B = b;
                        existing.L = Math.Round(l, 1);
                        existing.A_Lab = Math.Round(a_lab, 1);
                        existing.B_Lab = Math.Round(b_lab, 1);
                        existing.Group = groupId;
                        updated++;
                    }
                    else
                    {
                        _colorsData.Colors.Add(new BeadColor
                        {
                            ColorId = colorId,
                            Name = name,
                            R = r,
                            G = g,
                            B = b,
                            L = Math.Round(l, 1),
                            A_Lab = Math.Round(a_lab, 1),
                            B_Lab = Math.Round(b_lab, 1),
                            Group = groupId,
                            Enabled = true,
                            Favorite = false
                        });
                        imported++;
                    }
                }
            }

            if (imported > 0 || updated > 0)
                SaveColors();

            return (imported, updated, skippedDuplicates);
        }

        /// <summary>
        /// Deletes a custom color by ID.
        /// </summary>
        public bool DeleteColor(string colorId)
        {
            LoadColors();
            var color = _colorsData!.Colors.FirstOrDefault(c => c.ColorId == colorId);
            if (color != null)
            {
                _colorsData.Colors.Remove(color);
                SaveColors();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Deletes all colors belonging to a group and removes the group entry.
        /// </summary>
        public bool DeleteGroup(string groupId)
        {
            LoadColors();
            var colorsToRemove = _colorsData!.Colors.Where(c => c.Group == groupId).ToList();
            foreach (var c in colorsToRemove)
                _colorsData.Colors.Remove(c);

            var group = _colorsData.Groups.FirstOrDefault(g => g.GroupId == groupId);
            if (group != null)
                _colorsData.Groups.Remove(group);

            if (colorsToRemove.Count > 0 || group != null)
            {
                SaveColors();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if a pattern exists for the given image path and group filter.
        /// </summary>
        public bool PatternExists(string imagePath, string groupFilter = "all")
        {
            if (!File.Exists(_patternsPath)) return false;
            
            string json = File.ReadAllText(_patternsPath);
            using var doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.TryGetProperty("patterns", out var patterns))
            {
                foreach (var p in patterns.EnumerateArray())
                {
                    if (p.TryGetProperty("path", out var path) && path.GetString() == imagePath)
                    {
                        // Check if group filter matches
                        string patternGroup = "all";
                        if (p.TryGetProperty("group_filter", out var gf))
                            patternGroup = gf.GetString() ?? "all";
                        
                        if (patternGroup == groupFilter)
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Loads a pattern for the given image path and group filter.
        /// </summary>
        public PatternData? LoadPattern(string imagePath, string groupFilter = "all")
        {
            if (!File.Exists(_patternsPath)) return null;
            
            string json = File.ReadAllText(_patternsPath);
            using var doc = JsonDocument.Parse(json);
            
            if (!doc.RootElement.TryGetProperty("patterns", out var patterns))
                return null;

            foreach (var pEl in patterns.EnumerateArray())
            {
                if (!pEl.TryGetProperty("path", out var pathProp) || pathProp.GetString() != imagePath)
                    continue;
                
                // Check group filter
                string patternGroup = "all";
                if (pEl.TryGetProperty("group_filter", out var gf))
                    patternGroup = gf.GetString() ?? "all";
                
                if (patternGroup != groupFilter)
                    continue;
                
                var pattern = new PatternData { Path = imagePath, GroupFilter = patternGroup };
                
                if (pEl.TryGetProperty("colors", out var colors))
                {
                    foreach (var cEl in colors.EnumerateArray())
                    {
                        var pc = new PatternColor { ColorId = cEl.GetProperty("color_id").GetString() ?? "" };
                        
                        if (pEl.TryGetProperty("pixels", out var pixels))
                        {
                            foreach (var pxEl in pixels.EnumerateArray())
                            {
                                pc.Pixels.Add(new PixelCoord
                                {
                                    X = pxEl.GetProperty("x").GetInt32(),
                                    Y = pxEl.GetProperty("y").GetInt32()
                                });
                            }
                        }
                        pattern.Colors.Add(pc);
                    }
                }
                return pattern;
            }
            return null;
        }

        /// <summary>
        /// Generates a bead pattern from source image using specified group filter.
        /// </summary>
        public (PatternData pattern, BitmapSource image) GeneratePattern(
            BitmapSource source, string imagePath, int alphaThreshold, CompressionLevel compression, 
            ColorSpace colorSpace = ColorSpace.RGB, string groupFilter = "all")
        {
            // Force reload to get latest enabled/disabled state
            LoadColors(true);
            
            // Filter by enabled groups and specific group if not "all"
            var enabledGroups = _colorsData!.Groups.Where(g => g.Enabled).Select(g => g.GroupId).ToHashSet();
            var enabledColors = _colorsData.Colors
                .Where(c => c.Enabled && enabledGroups.Contains(c.Group))
                .Where(c => groupFilter == "all" || c.Group == groupFilter)
                .ToList();
            
            if (enabledColors.Count == 0)
                throw new InvalidOperationException("No enabled colors. Enable at least one color group.");

            var conv = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
            byte[] pixels = new byte[h * stride];
            conv.CopyPixels(pixels, stride, 0);

            // Extract unique colors
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

            // Match colors using selected color space
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
                    // Use Lab color space for distance calculation
                    RgbToLab(r, g, b, out double l, out double a_lab, out double b_lab);
                    topN = enabledColors
                        .Select(c => (c, dist: LabDistance(l, a_lab, b_lab, c.L, c.A_Lab, c.B_Lab)))
                        .OrderBy(x => x.dist)
                        .Take(n)
                        .ToList();
                }
                else
                {
                    // Use RGB color space for distance calculation (default)
                    topN = enabledColors
                        .Select(c => (c, dist: RgbDistance(r, g, b, c.R, c.G, c.B)))
                        .OrderBy(x => x.dist)
                        .Take(n)
                        .ToList();
                }

                // Priority: Already used > Favorite > Distance
                var match = topN.FirstOrDefault(x => usedIds.Contains(x.c.ColorId));
                if (match.c == null)
                    match = topN.FirstOrDefault(x => x.c.Favorite);
                if (match.c == null)
                    match = topN[0];
                
                mapping[kvp.Key] = match.c.ColorId;
                usedIds.Add(match.c.ColorId);
            }

            // Build pattern
            var colorDict = new Dictionary<string, PatternColor>();
            foreach (var kvp in uniqueColors)
            {
                string id = mapping[kvp.Key];
                if (!colorDict.ContainsKey(id))
                    colorDict[id] = new PatternColor { ColorId = id };
                colorDict[id].Pixels.AddRange(kvp.Value);
            }
            
            var pattern = new PatternData { Path = imagePath, GroupFilter = groupFilter, Colors = colorDict.Values.ToList() };

            // Generate image
            byte[] outPixels = new byte[h * stride];
            Array.Copy(pixels, outPixels, pixels.Length);
            
            var colorLookup = _colorsData.Colors.ToDictionary(c => c.ColorId);
            foreach (var pc in pattern.Colors)
            {
                if (!colorLookup.TryGetValue(pc.ColorId, out var bc)) continue;
                foreach (var px in pc.Pixels)
                {
                    int idx = px.Y * stride + px.X * 4;
                    outPixels[idx] = bc.B;
                    outPixels[idx + 1] = bc.G;
                    outPixels[idx + 2] = bc.R;
                }
            }
            
            var img = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, outPixels, stride);
            img.Freeze();

            SavePattern(pattern);
            return (pattern, img);
        }

        public BitmapSource GenerateImageFromPattern(PatternData pattern, BitmapSource source)
        {
            LoadColors();
            
            var conv = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
            byte[] pixels = new byte[h * stride];
            conv.CopyPixels(pixels, stride, 0);
            
            var colorLookup = _colorsData!.Colors.ToDictionary(c => c.ColorId);
            foreach (var pc in pattern.Colors)
            {
                if (!colorLookup.TryGetValue(pc.ColorId, out var bc)) continue;
                foreach (var px in pc.Pixels)
                {
                    int idx = px.Y * stride + px.X * 4;
                    if (idx + 3 < pixels.Length)
                    {
                        pixels[idx] = bc.B;
                        pixels[idx + 1] = bc.G;
                        pixels[idx + 2] = bc.R;
                    }
                }
            }
            
            var img = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            img.Freeze();
            return img;
        }

        private void SavePattern(PatternData pattern)
        {
            List<Dictionary<string, object>> patterns;
            
            if (File.Exists(_patternsPath))
            {
                string json = File.ReadAllText(_patternsPath);
                using var doc = JsonDocument.Parse(json);
                patterns = new List<Dictionary<string, object>>();
                
                if (doc.RootElement.TryGetProperty("patterns", out var arr))
                {
                    foreach (var p in arr.EnumerateArray())
                    {
                        // Keep patterns that don't match both path AND group_filter
                        bool samePath = p.TryGetProperty("path", out var pathProp) && pathProp.GetString() == pattern.Path;
                        string existingGroup = "all";
                        if (p.TryGetProperty("group_filter", out var gf))
                            existingGroup = gf.GetString() ?? "all";
                        
                        if (!(samePath && existingGroup == pattern.GroupFilter))
                        {
                            patterns.Add(JsonSerializer.Deserialize<Dictionary<string, object>>(p.GetRawText())!);
                        }
                    }
                }
            }
            else
            {
                patterns = new List<Dictionary<string, object>>();
            }

            var patternDict = new Dictionary<string, object>
            {
                ["path"] = pattern.Path,
                ["group_filter"] = pattern.GroupFilter,
                ["colors"] = pattern.Colors.Select(c => new Dictionary<string, object>
                {
                    ["color_id"] = c.ColorId,
                    ["pixels"] = c.Pixels.Select(px => new Dictionary<string, int>
                    {
                        ["x"] = px.X,
                        ["y"] = px.Y
                    }).ToList()
                }).ToList()
            };
            
            patterns.Add(patternDict);
            
            var root = new Dictionary<string, object> { ["patterns"] = patterns };
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_patternsPath, JsonSerializer.Serialize(root, options));
        }

        #region Color Conversion

        public static void RgbToLab(byte r, byte g, byte b, out double l, out double a, out double bLab)
        {
            // RGB to XYZ
            double rr = r / 255.0, gg = g / 255.0, bb = b / 255.0;
            
            rr = rr > 0.04045 ? Math.Pow((rr + 0.055) / 1.055, 2.4) : rr / 12.92;
            gg = gg > 0.04045 ? Math.Pow((gg + 0.055) / 1.055, 2.4) : gg / 12.92;
            bb = bb > 0.04045 ? Math.Pow((bb + 0.055) / 1.055, 2.4) : bb / 12.92;
            
            double x = (rr * 0.4124564 + gg * 0.3575761 + bb * 0.1804375) / 0.95047;
            double y = (rr * 0.2126729 + gg * 0.7151522 + bb * 0.0721750);
            double z = (rr * 0.0193339 + gg * 0.1191920 + bb * 0.9503041) / 1.08883;
            
            // XYZ to Lab
            x = x > 0.008856 ? Math.Pow(x, 1.0/3.0) : (7.787 * x) + (16.0/116.0);
            y = y > 0.008856 ? Math.Pow(y, 1.0/3.0) : (7.787 * y) + (16.0/116.0);
            z = z > 0.008856 ? Math.Pow(z, 1.0/3.0) : (7.787 * z) + (16.0/116.0);
            
            l = (116.0 * y) - 16.0;
            a = 500.0 * (x - y);
            bLab = 200.0 * (y - z);
        }

        public static double LabDistance(double l1, double a1, double b1, double l2, double a2, double b2)
        {
            double dl = l1 - l2;
            double da = a1 - a2;
            double db = b1 - b2;
            return Math.Sqrt(dl * dl + da * da + db * db);
        }

        /// <summary>
        /// Calculates the Euclidean distance between two colors in RGB space.
        /// </summary>
        public static double RgbDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            int dr = r1 - r2;
            int dg = g1 - g2;
            int db = b1 - b2;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        #endregion
    }
}
