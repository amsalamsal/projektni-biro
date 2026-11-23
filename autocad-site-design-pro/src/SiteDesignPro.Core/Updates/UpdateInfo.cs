using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace SiteDesignPro.Core.Updates
{
    /// <summary>
    /// A published version of the add-in (latest.json of the newest GitHub release): its number, what changed and where
    /// the AutoCAD bundle and the SketchUp extension are, with their SHA-256 so a damaged download is never installed.
    /// The same file is read by the SketchUp extension, so both sides always update to the same version.
    /// </summary>
    public sealed class UpdateInfo
    {
        /// <summary>Always the newest published release (GitHub redirects to its file).</summary>
        public const string LatestUrl = "https://github.com/amsalamsal/projektni-biro/releases/latest/download/latest.json";

        public string Version { get; private set; } = "0.0.0";

        public string Date { get; private set; } = string.Empty;

        public List<string> Notes { get; } = new List<string>();

        public string AutoCadUrl { get; private set; } = string.Empty;

        public string AutoCadSha256 { get; private set; } = string.Empty;

        public string SketchUpUrl { get; private set; } = string.Empty;

        public string SketchUpSha256 { get; private set; } = string.Empty;

        public string InstallerUrl { get; private set; } = string.Empty;

        /// <summary>Reads latest.json; throws <see cref="FormatException"/> when it is not a usable description of a version.</summary>
        public static UpdateInfo Parse(string json)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                var info = new UpdateInfo
                {
                    Version = Text(root, "version"),
                    Date = Text(root, "date"),
                    InstallerUrl = Text(root, "installer", "url"),
                    AutoCadUrl = Text(root, "autocad", "url"),
                    AutoCadSha256 = Text(root, "autocad", "sha256").ToLowerInvariant(),
                    SketchUpUrl = Text(root, "sketchup", "url"),
                    SketchUpSha256 = Text(root, "sketchup", "sha256").ToLowerInvariant(),
                };
                if (root.TryGetProperty("notes", out JsonElement notes) && notes.ValueKind == JsonValueKind.Array)
                {
                    info.Notes.AddRange(notes.EnumerateArray().Where(n => n.ValueKind == JsonValueKind.String).Select(n => n.GetString() ?? string.Empty).Where(n => n.Length > 0));
                }

                if (Parts(info.Version) == null || !info.AutoCadUrl.StartsWith("https://", StringComparison.Ordinal) || info.AutoCadSha256.Length != 64)
                {
                    throw new FormatException("opis verzije nije potpun (verzija, adresa ili SHA-256 paketa za AutoCAD)");
                }

                return info;
            }
            catch (JsonException ex)
            {
                throw new FormatException("opis verzije nije ispravan JSON: " + ex.Message, ex);
            }
        }

        /// <summary>Whether <paramref name="candidate"/> is a newer version than <paramref name="current"/> (major.minor.patch).</summary>
        public static bool IsNewer(string candidate, string current)
        {
            int[]? a = Parts(candidate), b = Parts(current);
            if (a == null || b == null)
            {
                return false;
            }

            for (int i = 0; i < 3; i++)
            {
                if (a[i] != b[i])
                {
                    return a[i] > b[i];
                }
            }

            return false;
        }

        /// <summary>The SHA-256 of a file, lowercase hex.</summary>
        public static string Sha256(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream s = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(s).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static int[]? Parts(string version)
        {
            string[] p = (version ?? string.Empty).Trim().TrimStart('v', 'V').Split('.');
            if (p.Length < 2 || p.Length > 4)
            {
                return null;
            }

            var r = new int[3];
            for (int i = 0; i < 3; i++)
            {
                if (i >= p.Length)
                {
                    continue;
                }

                if (!int.TryParse(p[i], NumberStyles.None, CultureInfo.InvariantCulture, out r[i]))
                {
                    return null;
                }
            }

            return r;
        }

        private static string Text(JsonElement e, params string[] path)
        {
            foreach (string key in path)
            {
                if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out e))
                {
                    return string.Empty;
                }
            }

            return e.ValueKind == JsonValueKind.String ? e.GetString() ?? string.Empty : string.Empty;
        }
    }
}
