using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using SiteDesignPro.Core.Catalog;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.Infrastructure
{
    /// <summary>Process-wide services: catalog, folders and diagnostic logging.</summary>
    internal static class AcadEnv
    {
        private static ElementCatalog? catalog;

        public static string Version => typeof(AcadEnv).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        /// <summary>Folder of the loaded plug-in DLL (inside the .bundle Contents folder).</summary>
        public static string PluginDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;

        /// <summary>%APPDATA%\SiteDesignPro - user catalogs and preferences.</summary>
        public static string UserDirectory
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SiteDesignPro");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string UserCatalogDirectory
        {
            get
            {
                string dir = Path.Combine(UserDirectory, "catalogs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>
        /// Catalog folder shipped in the bundle (SiteDesignPro.bundle/Resources/catalog, three levels above
        /// Contents/Win64/Rxx); the catalog embedded in the DLL is used when it is missing.
        /// </summary>
        public static string BundleCatalogDirectory
        {
            get
            {
                foreach (string rel in new[] { Path.Combine("..", "..", "..", "Resources", "catalog"), Path.Combine("..", "Resources", "catalog") })
                {
                    string dir = Path.GetFullPath(Path.Combine(PluginDirectory, rel));
                    if (File.Exists(Path.Combine(dir, "element-types.json")))
                    {
                        return dir;
                    }
                }

                return Path.GetFullPath(Path.Combine(PluginDirectory, "..", "..", "..", "Resources", "catalog"));
            }
        }

        public static ElementCatalog Catalog
        {
            get
            {
                if (catalog == null)
                {
                    catalog = ElementCatalog.LoadWithOverlays(BundleCatalogDirectory, UserCatalogDirectory);
                    foreach (string m in catalog.LoadMessages)
                    {
                        Log.Info(m);
                    }
                }

                return catalog;
            }
        }

        public static void ReloadCatalog()
        {
            catalog = null;
            _ = Catalog;
        }

        public static Document? ActiveDocument => AcApp.DocumentManager.MdiActiveDocument;

        public static Editor? Editor => ActiveDocument?.Editor;

        public static void Message(string text)
        {
            Editor?.WriteMessage("\n" + text);
        }
    }

    /// <summary>Dnevnik (log) u %LOCALAPPDATA%\SiteDesignPro\logs, jedna datoteka po danu.</summary>
    internal static class Log
    {
        private static readonly object Gate = new object();

        public static string FilePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SiteDesignPro", "logs");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "site-design-pro-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
            }
        }

        public static void Info(string message) => Write("INFO", message);

        /// <summary>
        /// Runs an event handler (palette, ribbon, document events) so that an error is logged and reported instead of
        /// reaching AutoCAD, where an unhandled exception in an event shows the crash dialog or closes AutoCAD.
        /// </summary>
        public static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Error(what + " nije uspjelo", ex);
            }
        }

        public static void Warn(string message) => Write("WARN", message);

        /// <summary>Full details go to the log file; the command line shows a short Bosnian message.</summary>
        public static void Error(string message, Exception? ex = null) => Write("ERROR", ex == null ? message : message + ": " + ex, ex == null ? message : message + ": " + ex.Message);

        private static void Write(string level, string message, string? shortMessage = null)
        {
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " [" + level + "] " + message + Environment.NewLine);
                }
            }
            catch (IOException)
            {
                // Logging must never break a command; the message still reaches the command line below.
            }
            catch (UnauthorizedAccessException)
            {
            }

            if (level != "INFO")
            {
                AcadEnv.Message("Vanjsko uređenje PRO – " + (level == "ERROR" ? "greška" : "upozorenje") + ": " + (shortMessage ?? message) +
                    (level == "ERROR" ? " (detalji: " + FilePath + ")" : string.Empty));
            }
        }
    }
}
