using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SiteDesignPro.AutoCAD.Infrastructure;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.UI
{
    /// <summary>Per-user preferences (favourite and recently used element types) in %APPDATA%\SiteDesignPro.</summary>
    internal sealed class Preferences
    {
        private static Preferences? current;

        public List<string> Favorites { get; set; } = new List<string>();

        public List<string> Recent { get; set; } = new List<string>();

        /// <summary>Last width (m) used per path type, offered as the default the next time.</summary>
        public Dictionary<string, double> Widths { get; set; } = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>Last answers of the project window (sheet title, scale) and other small defaults.</summary>
        public Dictionary<string, string> Defaults { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Version of the palette's first-show layout already applied (docked right); 0 = never.</summary>
        public int PaletteLayout { get; set; }

        private static string FilePath => Path.Combine(AcadEnv.UserDirectory, "preferences.json");

        public static Preferences Current => current ??= Load();

        private static Preferences Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new Preferences();
                }
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException)
            {
                Log.Warn("Korisničke postavke nisu pročitane i vraćene su na zadane: " + ex.Message);
            }

            return new Preferences();
        }

        public void Save()
        {
            try
            {
                // Atomic: an interrupted write (power loss, crash) must not leave a truncated or zero-filled file.
                Core.Safety.SafeFile.WriteAllTextAtomic(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Warn("Korisničke postavke nisu sačuvane: " + ex.Message);
            }
        }

        public void Used(string typeId)
        {
            Recent.Remove(typeId);
            Recent.Insert(0, typeId);
            if (Recent.Count > 12)
            {
                Recent.RemoveRange(12, Recent.Count - 12);
            }

            Save();
        }

        public double? LastWidth(string typeId) => Widths != null && Widths.TryGetValue(typeId, out double w) && w > 0 ? w : (double?)null;

        public void RememberWidth(string typeId, double width)
        {
            Widths ??= new Dictionary<string, double>(StringComparer.Ordinal);
            Widths[typeId] = width;
            Save();
        }

        public string Default(string key, string fallback) => Defaults != null && Defaults.TryGetValue(key, out string? v) && !string.IsNullOrEmpty(v) ? v : fallback;

        public void RememberDefault(string key, string value)
        {
            Defaults ??= new Dictionary<string, string>(StringComparer.Ordinal);
            Defaults[key] = value;
            Save();
        }

        public void ToggleFavorite(string typeId)
        {
            if (!Favorites.Remove(typeId))
            {
                Favorites.Add(typeId);
            }

            Save();
        }
    }

    /// <summary>Loads the embedded icon family (PNG, light and dark variants, 16 and 32 px).</summary>
    internal static class Icons
    {
        private static readonly Dictionary<string, ImageSource?> Cache = new Dictionary<string, ImageSource?>(StringComparer.Ordinal);

        /// <summary>True when AutoCAD uses the dark theme (COLORTHEME = 0).</summary>
        public static bool DarkTheme
        {
            get
            {
                try
                {
                    return Convert.ToInt32(AcApp.GetSystemVariable("COLORTHEME"), System.Globalization.CultureInfo.InvariantCulture) == 0;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    return false;
                }
            }
        }

        public static ImageSource? Get(string name, int size)
        {
            string key = name + "_" + size + (DarkTheme ? "_dark" : string.Empty);
            if (Cache.TryGetValue(key, out ImageSource? cached))
            {
                return cached;
            }

            ImageSource? img = Load(key) ?? Load(name + "_" + size) ?? Load("generic_" + size + (DarkTheme ? "_dark" : string.Empty));
            Cache[key] = img;
            return img;
        }

        public static void ClearCache() => Cache.Clear();

        private static ImageSource? Load(string key)
        {
            Stream? s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SiteDesignPro.Icons." + key + ".png");
            if (s == null)
            {
                return null;
            }

            using (s)
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = s;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
        }
    }
}
