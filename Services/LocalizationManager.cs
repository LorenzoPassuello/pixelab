// ============================================================================
// LocalizationManager.cs
// Manages application localization using JSON language files.
// Provides a singleton instance for accessing translated strings throughout the app.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Pixelab
{
    /// <summary>
    /// Singleton class that manages application localization.
    /// Loads language strings from JSON files and provides translation lookup.
    /// 
    /// Usage:
    ///   LocalizationManager.Instance.LoadLanguage("it");
    ///   string text = LocalizationManager.Instance.T("menu.file");
    /// </summary>
    public class LocalizationManager
    {
        // ============================================================================
        // SINGLETON PATTERN
        // ============================================================================
        
        /// <summary>Singleton instance backing field.</summary>
        private static LocalizationManager? _instance;
        
        /// <summary>Gets the singleton instance of the LocalizationManager.</summary>
        public static LocalizationManager Instance => _instance ??= new LocalizationManager();

        // ============================================================================
        // PRIVATE FIELDS
        // ============================================================================
        
        /// <summary>
        /// Dictionary storing all loaded translation strings.
        /// Keys are flattened using dot notation (e.g., "menu.file", "labels.dimensions").
        /// Values are the actual translated strings (extracted immediately to avoid disposal issues).
        /// </summary>
        private Dictionary<string, string> _strings = new();
        
        /// <summary>Currently loaded language code (e.g., "en", "it", "fr", "es").</summary>
        private string _currentLanguage = "en";
        
        /// <summary>Path to the Languages folder containing JSON language files.</summary>
        private string _languagesPath = "";

        // ============================================================================
        // EVENTS
        // ============================================================================
        
        /// <summary>
        /// Event fired when the language is changed.
        /// UI components can subscribe to this to refresh their localized text.
        /// </summary>
        public event Action? LanguageChanged;

        // ============================================================================
        // PUBLIC PROPERTIES
        // ============================================================================
        
        /// <summary>Gets the currently loaded language code.</summary>
        public string CurrentLanguage => _currentLanguage;
        
        /// <summary>Gets the display name of the current language (e.g., "English", "Italiano").</summary>
        public string CurrentLanguageName => T("language_name");

        // ============================================================================
        // CONSTRUCTOR
        // ============================================================================
        
        /// <summary>
        /// Private constructor for singleton pattern.
        /// Initializes the path to the Languages folder.
        /// </summary>
        private LocalizationManager()
        {
            // Find the Languages folder - try multiple possible locations
            FindLanguagesPath();
        }

        /// <summary>
        /// Searches for the Languages folder in multiple possible locations.
        /// Sets _languagesPath to the first valid location found.
        /// </summary>
        private void FindLanguagesPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // List of possible paths to check (in order of priority)
            var possiblePaths = new[]
            {
                // Standard install location
                Path.Combine(baseDir, "Resources", "Languages"),
                // Development location (relative path)
                Path.Combine("Resources", "Languages"),
                // Alternative: directly in base directory
                Path.Combine(baseDir, "Languages"),
                // Alternative: parent directory (for some build configurations)
                Path.Combine(Directory.GetParent(baseDir)?.FullName ?? baseDir, "Resources", "Languages")
            };
            
            // Find the first path that exists and contains JSON files
            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    try
                    {
                        var jsonFiles = Directory.GetFiles(path, "*.json");
                        if (jsonFiles.Length > 0)
                        {
                            _languagesPath = path;
                            return;
                        }
                    }
                    catch
                    {
                        // Ignore access errors, try next path
                    }
                }
            }
            
            // Default fallback (may not exist)
            _languagesPath = possiblePaths[0];
        }

        // ============================================================================
        // PUBLIC METHODS
        // ============================================================================
        
        /// <summary>
        /// Loads a language file by its language code.
        /// If the specified language file doesn't exist, falls back to English.
        /// </summary>
        /// <param name="languageCode">Language code (e.g., "en", "it", "fr", "es")</param>
        public void LoadLanguage(string languageCode)
        {
            // Re-check path in case files were added after startup
            bool needsPathRefresh = string.IsNullOrEmpty(_languagesPath) || !Directory.Exists(_languagesPath);
            if (!needsPathRefresh)
            {
                try
                {
                    needsPathRefresh = Directory.GetFiles(_languagesPath, "*.json").Length == 0;
                }
                catch
                {
                    needsPathRefresh = true;
                }
            }
            
            if (needsPathRefresh)
            {
                FindLanguagesPath();
            }
            
            // Build path to the language file
            string filePath = Path.Combine(_languagesPath, $"{languageCode}.json");
            
            // Fallback to English if the requested language doesn't exist
            if (!File.Exists(filePath))
            {
                filePath = Path.Combine(_languagesPath, "en.json");
                languageCode = "en";
            }

            // If even English doesn't exist, we can't do anything
            if (!File.Exists(filePath))
                return;

            try
            {
                // Read and parse the JSON file
                string json = File.ReadAllText(filePath);
                using var doc = JsonDocument.Parse(json);
                
                // Clear existing strings and parse the new language file
                _strings.Clear();
                ParseElement(doc.RootElement, "");
                _currentLanguage = languageCode;
                
                // Notify subscribers that the language has changed
                LanguageChanged?.Invoke();
            }
            catch
            {
                // Silently fail - keep existing strings if loading fails
            }
        }

        /// <summary>
        /// Gets a translated string by its key.
        /// Keys use dot notation for nested values (e.g., "menu.file", "labels.dimensions").
        /// </summary>
        /// <param name="key">The translation key</param>
        /// <returns>The translated string, or the key itself if not found</returns>
        public string T(string key)
        {
            if (_strings.TryGetValue(key, out var value))
            {
                return value;
            }
            
            // Return the key itself if translation is not found
            // This helps identify missing translations during development
            return key;
        }

        /// <summary>
        /// Gets a translated string with parameter substitution.
        /// Uses string.Format internally, so placeholders should be {0}, {1}, etc.
        /// </summary>
        /// <param name="key">The translation key</param>
        /// <param name="args">Values to substitute into the string</param>
        /// <returns>The translated and formatted string</returns>
        /// <example>
        /// // In language file: "pattern_uses_colors": "Pattern uses {0} colors"
        /// string text = Loc.T("labels.pattern_uses_colors", 15);
        /// // Result: "Pattern uses 15 colors"
        /// </example>
        public string T(string key, params object[] args)
        {
            string template = T(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                // Return unformatted template if formatting fails
                return template;
            }
        }

        /// <summary>
        /// Gets a list of all available languages.
        /// Scans the Languages folder for JSON files and reads their metadata.
        /// </summary>
        /// <returns>List of available languages with their codes and display names</returns>
        public List<LanguageInfo> GetAvailableLanguages()
        {
            var languages = new List<LanguageInfo>();
            
            // Re-check path in case files were added after startup
            bool needsPathRefresh = string.IsNullOrEmpty(_languagesPath) || !Directory.Exists(_languagesPath);
            if (!needsPathRefresh)
            {
                try
                {
                    needsPathRefresh = Directory.GetFiles(_languagesPath, "*.json").Length == 0;
                }
                catch
                {
                    needsPathRefresh = true;
                }
            }
            
            if (needsPathRefresh)
            {
                FindLanguagesPath();
            }
            
            // Check if Languages folder exists
            if (!Directory.Exists(_languagesPath))
                return languages;

            // Scan all JSON files in the Languages folder
            try
            {
                foreach (var file in Directory.GetFiles(_languagesPath, "*.json"))
                {
                    try
                    {
                        // Read the file and extract language metadata
                        string json = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(json);
                        
                        // Get the language code from the filename
                        string code = Path.GetFileNameWithoutExtension(file);
                        string name = code; // Default to code if name not found
                        
                        // Try to get the display name from the file
                        if (doc.RootElement.TryGetProperty("language_name", out var nameProp))
                            name = nameProp.GetString() ?? code;
                        
                        languages.Add(new LanguageInfo { Code = code, Name = name });
                    }
                    catch
                    {
                        // Skip files that can't be parsed
                    }
                }
            }
            catch
            {
                // Ignore directory access errors
            }

            return languages;
        }

        // ============================================================================
        // PRIVATE METHODS
        // ============================================================================
        
        /// <summary>
        /// Recursively parses a JSON element and flattens it into the strings dictionary.
        /// Nested objects become dot-separated keys.
        /// IMPORTANT: This extracts string values immediately to avoid JsonDocument disposal issues.
        /// </summary>
        /// <param name="element">The JSON element to parse</param>
        /// <param name="prefix">Current key prefix (empty for root)</param>
        /// <example>
        /// JSON: { "menu": { "file": "File", "view": "View" } }
        /// Results in: _strings["menu.file"] = "File", _strings["menu.view"] = "View"
        /// </example>
        private void ParseElement(JsonElement element, string prefix)
        {
            foreach (var property in element.EnumerateObject())
            {
                // Build the full key by combining prefix with property name
                string key = string.IsNullOrEmpty(prefix) 
                    ? property.Name 
                    : $"{prefix}.{property.Name}";
                
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    // Recursively parse nested objects
                    ParseElement(property.Value, key);
                }
                else if (property.Value.ValueKind == JsonValueKind.String)
                {
                    // Extract and store the string value IMMEDIATELY
                    // This is crucial - we must get the string before JsonDocument is disposed
                    string? stringValue = property.Value.GetString();
                    _strings[key] = stringValue ?? key;
                }
            }
        }

        // ============================================================================
        // NESTED CLASSES
        // ============================================================================
        
        /// <summary>
        /// Represents information about an available language.
        /// </summary>
        public class LanguageInfo
        {
            /// <summary>Language code (e.g., "en", "it", "fr")</summary>
            public string Code { get; set; } = "";
            
            /// <summary>Display name of the language (e.g., "English", "Italiano")</summary>
            public string Name { get; set; } = "";
        }
    }
}
