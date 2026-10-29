using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Safety;
using SiteDesignPro.Core.Text;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>Manual backups, recovery of interrupted operations and help.</summary>
    public sealed class SafetyCommands
    {
        /// <summary>Makes a verified backup of the last saved version of the drawing now.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPKOPIJA", CommandFlags.Modal)]
        public void Backup() => CommandContext.Run("SDPKOPIJA", ctx =>
        {
            BackupPolicy policy = ctx.Settings.Backups;
            if (!policy.Enabled && ctx.Keyword("Automatske kopije su isključene. Napraviti ručnu kopiju svejedno? [Da/Ne]", new[] { "Da", "Ne" }, "Da") == "Ne")
            {
                return;
            }

            var manual = new BackupPolicy { Enabled = true, KeepPerFile = policy.KeepPerFile, MaxAgeDays = policy.MaxAgeDays, Folder = policy.Folder };
            SafetyHost.BackupDrawing(ctx.Db, manual, "rucna kopija", out string note);
            ctx.Write(note);
            ListBackups(ctx.Ed, ctx.Db.Filename, manual, 8);
        });

        /// <summary>Settings for automatic backups (on/off, number of copies, maximum age).</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPKOPIJEPOSTAVKE", CommandFlags.Modal)]
        public void BackupSettings() => CommandContext.Run("SDPKOPIJEPOSTAVKE", ctx =>
        {
            BackupPolicy p = ctx.Settings.Backups;
            p.Enabled = ctx.Keyword("Automatska sigurnosna kopija prije većih operacija [Ukljuceno/Iskljuceno]", new[] { "Ukljuceno", "Iskljuceno" }, p.Enabled ? "Ukljuceno" : "Iskljuceno") == "Ukljuceno";
            p.KeepPerFile = ctx.Integer("Broj kopija po crtežu (najnovija se nikad ne briše)", p.KeepPerFile, 1);
            p.MaxAgeDays = ctx.Integer("Najveća starost kopija u danima (0 = bez ograničenja)", p.MaxAgeDays, 0);
            ctx.SaveSettings();
            ctx.Write(Bs.T("Sigurnosne kopije: {0}, čuva se {1} po crtežu, najviše {2} dana. Folder: {3}", p.Enabled ? "uključene" : "isključene", p.KeepPerFile,
                p.MaxAgeDays == 0 ? "neograničeno" : p.MaxAgeDays.ToString(System.Globalization.CultureInfo.InvariantCulture), BackupManager.DefaultFolderName + " pored crteža"));
        });

        /// <summary>Interrupted operations and backups; opens a backup as a new drawing (never overwrites the current one).</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPOPORAVAK", CommandFlags.Session)]
        public void Recovery()
        {
            Document? doc = AcApp.DocumentManager.MdiActiveDocument;
            Editor? ed = doc?.Editor;
            if (doc == null || ed == null)
            {
                return;
            }

            try
            {
                List<JournalEntry> open = SafetyHost.Unfinished();
                ed.WriteMessage("\n" + (open.Count == 0 ? "Nema prekinutih operacija." : Bs.T("Prekinute operacije ({0}):", open.Count)));
                foreach (JournalEntry e in open)
                {
                    ed.WriteMessage("\n  " + RecoveryJournal.Describe(e));
                }

                string file = doc.Database.Filename;
                ProjectSettings? settings = ReadSettings(doc);
                BackupPolicy policy = SafetyHost.PolicyFor(settings);
                List<BackupInfo> backups = string.IsNullOrEmpty(file) || !File.Exists(file) ? new List<BackupInfo>() : new BackupManager(policy).List(file);
                ListBackups(ed, file, policy, 10);

                var o = new PromptKeywordOptions("\nOporavak [Otvori/Folder/Oznaci/Izlaz]");
                foreach (string k in new[] { "Otvori", "Folder", "Oznaci", "Izlaz" })
                {
                    o.Keywords.Add(k);
                }

                o.Keywords.Default = "Izlaz";
                o.AllowNone = true;
                PromptResult r = ed.GetKeywords(o);
                string choice = r.Status == PromptStatus.OK ? r.StringResult : "Izlaz";
                switch (choice)
                {
                    case "Otvori":
                        {
                            if (backups.Count == 0)
                            {
                                ed.WriteMessage("\nNema sigurnosnih kopija ovog crteža.");
                                return;
                            }

                            var io = new PromptIntegerOptions("\nBroj kopije s liste (1 = najnovija)") { LowerLimit = 1, UpperLimit = backups.Count, DefaultValue = 1, UseDefaultValue = true };
                            PromptIntegerResult ir = ed.GetInteger(io);
                            if (ir.Status != PromptStatus.OK)
                            {
                                return;
                            }

                            string restored = new BackupManager(policy).RestoreAsCopy(backups[ir.Value - 1], file);
                            ed.WriteMessage("\nKopija je vraćena kao novi crtež: " + restored + " (trenutni crtež nije mijenjan).");
                            AcApp.DocumentManager.Open(restored, false);
                            break;
                        }

                    case "Folder":
                        {
                            string folder = string.IsNullOrEmpty(file) ? AcadEnv.UserDirectory : new BackupManager(policy).FolderFor(file);
                            Directory.CreateDirectory(folder);
                            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
                            break;
                        }

                    case "Oznaci":
                        foreach (JournalEntry e in open)
                        {
                            SafetyHost.End(e.OperationId, JournalState.RolledBack, "Pregledao korisnik (SDPOPORAVAK).");
                        }

                        ed.WriteMessage(Bs.T("\nOznačeno kao pregledano: {0}.", open.Count));
                        break;
                }
            }
            catch (System.Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is Autodesk.AutoCAD.Runtime.Exception || ex is InvalidOperationException)
            {
                Log.Error("SDPOPORAVAK nije uspio", ex);
            }
        }

        /// <summary>Opens the Bosnian user documentation.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPOMOC", CommandFlags.Modal)]
        public void Help()
        {
            string docs = Path.GetFullPath(Path.Combine(AcadEnv.PluginDirectory, "..", "..", "Docs", "index.html"));
            if (!File.Exists(docs))
            {
                AcadEnv.Message("Dokumentacija nije pronađena (" + docs + "). Pogledajte folder docs/ u izvornom paketu.");
                return;
            }

            Process.Start(new ProcessStartInfo(docs) { UseShellExecute = true });
        }

        /// <summary>Reports interrupted operations once, after AutoCAD starts (called from the plug-in initialisation).</summary>
        internal static void ReportInterrupted()
        {
            List<JournalEntry> open = SafetyHost.Unfinished();
            if (open.Count == 0)
            {
                return;
            }

            AcadEnv.Message(Bs.T("Vanjsko uređenje PRO: pronađeno {0} prekinutih operacija (npr. AutoCAD je zatvoren tokom rada). AutoCAD nedovršenu naredbu ne čuva; " +
                "ako crtež nije ispravan, otvorite sigurnosnu kopiju naredbom SDPOPORAVAK.", open.Count));
            foreach (JournalEntry e in open.Skip(Math.Max(0, open.Count - 3)))
            {
                AcadEnv.Message("  " + RecoveryJournal.Describe(e));
            }
        }

        private static void ListBackups(Editor ed, string file, BackupPolicy policy, int max)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                ed.WriteMessage("\nCrtež nije sačuvan, pa nema sigurnosnih kopija.");
                return;
            }

            List<BackupInfo> list = new BackupManager(policy).List(file);
            ed.WriteMessage("\n" + (list.Count == 0 ? "Nema sigurnosnih kopija ovog crteža." : Bs.T("Sigurnosne kopije ({0}, najnovija prva):", list.Count)));
            int i = 0;
            foreach (BackupInfo b in list.Take(max))
            {
                ed.WriteMessage(Bs.T("\n  {0}. {1:dd.MM.yyyy. HH:mm:ss}  {2}  ({3} kB)", ++i, b.CreatedUtc.ToLocalTime(), Path.GetFileName(b.Path), (b.Size / 1024).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        private static ProjectSettings? ReadSettings(Document doc)
        {
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                ProjectSettings? s = ProjectStore.Read(doc.Database, tr);
                tr.Commit();
                return s;
            }
        }
    }
}
