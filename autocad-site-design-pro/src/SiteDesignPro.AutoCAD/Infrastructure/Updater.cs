using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using SiteDesignPro.Core.Updates;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.Infrastructure
{
    /// <summary>
    /// Automatic updates from the GitHub releases of the project. Once a day, in the background, latest.json is read; a
    /// newer version is offered in a window (or, in the automatic mode, just downloaded). The new bundle is downloaded,
    /// checked (SHA-256) and unpacked next to the settings; AutoCAD keeps the add-in's files locked while it runs, so a
    /// small hidden PowerShell script waits until every AutoCAD is closed and only then copies the new bundle over the
    /// installed one. The next start reports the result. Settings and drawings are never touched.
    /// </summary>
    internal static class Updater
    {
        public const string Ask = "ask", Auto = "auto", Off = "off";
        private static readonly TimeSpan Interval = TimeSpan.FromHours(20);
        private static volatile UpdateInfo? found;
        private static string? scheduledVersion;
        private static bool idleHooked;

        public static string Folder
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SiteDesignPro", "update");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>ask (default): offer each new version; auto: download and install on closing; off: only on request.</summary>
        public static string Mode
        {
            get
            {
                string m = UI.Preferences.Current.Default("updates", Ask);
                return m == Auto || m == Off ? m : Ask;
            }

            set => UI.Preferences.Current.RememberDefault("updates", value);
        }

        public static string ModeText(string mode) => mode == Auto ? "automatski (instalira se pri zatvaranju AutoCAD-a)" : mode == Off ? "isključeno (samo na zahtjev)" : "pita prije instalacije";

        /// <summary>The installed bundle (…\SiteDesignPro.bundle), or null when the add-in does not run from a bundle.</summary>
        public static string? BundleDirectory
        {
            get
            {
                string dir = Path.GetFullPath(Path.Combine(AcadEnv.PluginDirectory, "..", "..", ".."));
                return dir.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(dir, "PackageContents.xml")) ? dir : null;
            }
        }

        /// <summary>At start: the result of the last installation, then (once a day) a check in the background.</summary>
        public static void Startup()
        {
            ReportLastRun();
            if (Mode == Off)
            {
                return;
            }

            string last = UI.Preferences.Current.Default("updates:lastCheck", string.Empty);
            if (DateTime.TryParse(last, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime when) && DateTime.UtcNow - when < Interval)
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    UpdateInfo info = Fetch();
                    if (UpdateInfo.IsNewer(info.Version, AcadEnv.Version) && info.Version != UI.Preferences.Current.Default("updates:skip", string.Empty))
                    {
                        found = info;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("Provjera ažuriranja nije uspjela: " + ex.Message);
                }
            });
            if (!idleHooked)
            {
                idleHooked = true;
                AcApp.Idle += OnIdle;
            }
        }

        /// <summary>Reads latest.json of the newest release (throws on a network or format problem).</summary>
        public static UpdateInfo Fetch()
        {
            using HttpClient http = Client();
            string json = http.GetStringAsync(UpdateInfo.LatestUrl).GetAwaiter().GetResult();
            UI.Preferences.Current.RememberDefault("updates:lastCheck", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            return UpdateInfo.Parse(json);
        }

        /// <summary>Downloads, checks and unpacks the new bundle, then starts the script that installs it when AutoCAD closes.</summary>
        public static void Prepare(UpdateInfo info)
        {
            string? target = BundleDirectory;
            if (target == null)
            {
                throw new InvalidOperationException("dodatak nije pokrenut iz instaliranog paketa (SiteDesignPro.bundle), pa se ne može sam ažurirati");
            }

            if (scheduledVersion == info.Version)
            {
                return;
            }

            string dir = Path.Combine(Folder, info.Version);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }

            Directory.CreateDirectory(dir);
            string zip = Path.Combine(dir, "SiteDesignPro.bundle.zip");
            using (HttpClient http = Client())
            using (Stream s = http.GetStreamAsync(info.AutoCadUrl).GetAwaiter().GetResult())
            using (FileStream f = File.Create(zip))
            {
                s.CopyTo(f);
            }

            string sha = UpdateInfo.Sha256(zip);
            if (!string.Equals(sha, info.AutoCadSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("preuzeti paket je oštećen (SHA-256 se ne slaže), ništa nije instalirano");
            }

            string unpacked = Path.Combine(dir, "novo");
            ZipFile.ExtractToDirectory(zip, unpacked);
            string? source = Directory.GetFiles(unpacked, "PackageContents.xml", SearchOption.AllDirectories).Select(Path.GetDirectoryName).FirstOrDefault();
            if (source == null)
            {
                throw new InvalidDataException("u preuzetom paketu nema PackageContents.xml");
            }

            string script = Path.Combine(Folder, "instaliraj-azuriranje.ps1");
            File.WriteAllText(script, Script, new UTF8Encoding(true));
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\" -Source \"" + source + "\" -Target \"" + target + "\" -Version \"" + info.Version + "\" -Folder \"" + Folder + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            Process.Start(psi);
            scheduledVersion = info.Version;
            Log.Info("Ažuriranje " + info.Version + " pripremljeno; instalira se kad se zatvori AutoCAD.");
        }

        /// <summary>The window offering a version: what is new, Instaliraj / Preskoči / Kasnije. Returns the chosen action.</summary>
        public static string Offer(UpdateInfo info, bool manual)
        {
            var rows = new List<(string, string)> { ("Instalirana verzija", AcadEnv.Version), ("Nova verzija", info.Version) };
            if (info.Date.Length > 0)
            {
                rows.Add(("Objavljena", info.Date));
            }

            var notes = info.Notes.Take(14).ToList();
            notes.Add("Instalira se kad zatvorite AutoCAD (dok radi, njegove datoteke su zaključane). Crteži i postavke ostaju. SketchUp dio se ažurira sam u SketchUpu.");
            var buttons = new List<(string, string)> { ("install", "Instaliraj kad zatvorim AutoCAD") };
            if (!manual)
            {
                buttons.Add(("skip", "Preskoči ovu verziju"));
            }

            buttons.Add(("later", "Kasnije"));
            return UI.InfoDialog.Present("Ažuriranje", "Dostupna je nova verzija " + info.Version, rows, notes, null, buttons.ToArray());
        }

        /// <summary>Installs the chosen version (download + scheduled copy) and says so; errors go into a window and the log.</summary>
        public static bool Install(UpdateInfo info)
        {
            try
            {
                Prepare(info);
                AcadEnv.Message("Vanjsko uređenje PRO " + info.Version + " je preuzeto i instalira se kad zatvorite AutoCAD.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Ažuriranje nije pripremljeno", ex);
                UI.InfoDialog.Present("Ažuriranje", "Ažuriranje nije uspjelo", Array.Empty<(string, string)>(),
                    new[] { "Instalirana verzija radi i dalje. Novu verziju možete instalirati i ručno: instalacioni zip sa stranice izdanja." }, new[] { ex.Message });
                return false;
            }
        }

        private static void OnIdle(object? sender, EventArgs e)
        {
            UpdateInfo? info = found;
            Document? doc = AcApp.DocumentManager.MdiActiveDocument;
            if (info == null || doc == null || !string.IsNullOrEmpty(doc.CommandInProgress))
            {
                return;
            }

            found = null;
            AcApp.Idle -= OnIdle;
            idleHooked = false;
            Log.Guard("Ponuda ažuriranja", () =>
            {
                if (Mode == Auto)
                {
                    Install(info);
                    return;
                }

                string choice = Offer(info, false);
                if (choice == "install")
                {
                    Install(info);
                }
                else if (choice == "skip")
                {
                    UI.Preferences.Current.RememberDefault("updates:skip", info.Version);
                }
            });
        }

        /// <summary>What the installation script left after the last closing of AutoCAD.</summary>
        private static void ReportLastRun()
        {
            string ok = Path.Combine(Folder, "instalirano.txt"), failed = Path.Combine(Folder, "greska.txt");
            if (File.Exists(ok))
            {
                AcadEnv.Message("Vanjsko uređenje PRO je ažurirano na verziju " + File.ReadAllText(ok).Trim() + ".");
                Log.Info("Ažurirano na " + File.ReadAllText(ok).Trim());
                File.Delete(ok);
            }

            if (File.Exists(failed))
            {
                string text = File.ReadAllText(failed).Trim();
                AcadEnv.Message("Ažuriranje nije instalirano: " + text + " Pokušajte ponovo (Pomoć → Ažuriranja) ili instalirajte zip ručno.");
                Log.Warn("Ažuriranje nije instalirano: " + text);
                File.Delete(failed);
            }
        }

        private static HttpClient Client()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("VanjskoUredjenjePRO/" + AcadEnv.Version);
            return http;
        }

        /// <summary>Waits until no AutoCAD runs, then mirrors the new bundle over the installed one (only a *.bundle folder).</summary>
        private const string Script = @"param([string]$Source, [string]$Target, [string]$Version, [string]$Folder)
$ErrorActionPreference = 'Stop'
$deadline = (Get-Date).AddDays(14)
while (Get-Process -Name acad -ErrorAction SilentlyContinue) {
    if ((Get-Date) -gt $deadline) { exit 1 }
    Start-Sleep -Seconds 5
}
Start-Sleep -Seconds 3
try {
    if (-not $Target.ToLower().EndsWith('.bundle')) { throw ""ciljni folder nije paket dodatka: $Target"" }
    if (-not (Test-Path (Join-Path $Source 'PackageContents.xml'))) { throw ""nova verzija nije potpuna: $Source"" }
    & robocopy $Source $Target /MIR /R:10 /W:3 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw ""kopiranje nije uspjelo (robocopy $LASTEXITCODE) - je li AutoCAD i dalje otvoren?"" }
    Set-Content -Path (Join-Path $Folder 'instalirano.txt') -Value $Version -Encoding UTF8
    Remove-Item -Recurse -Force (Split-Path $Source -Parent) -ErrorAction SilentlyContinue
} catch {
    Set-Content -Path (Join-Path $Folder 'greska.txt') -Value ($Version + ': ' + $_.Exception.Message) -Encoding UTF8
}
";
    }
}
