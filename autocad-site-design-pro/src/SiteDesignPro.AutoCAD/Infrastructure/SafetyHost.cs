using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Safety;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.AutoCAD.Infrastructure
{
    /// <summary>
    /// Backups and recovery journal for AutoCAD. Before a major operation the last saved version of the drawing
    /// is copied (verified) to "_SDP_backup" next to the DWG, and the operation is journalled. Unsaved changes are
    /// protected by AutoCAD UNDO: every command runs in one transaction. The source DWG is never overwritten.
    /// </summary>
    internal static class SafetyHost
    {
        private static RecoveryJournal? journal;

        public static RecoveryJournal Journal => journal ??= new RecoveryJournal(Path.Combine(AcadEnv.UserDirectory, "autocad-journal.jsonl"));

        /// <summary>Backs up the saved drawing file. Returns the backup path, or null with the reason in <paramref name="note"/>.</summary>
        public static string? BackupDrawing(Database db, BackupPolicy policy, string operation, out string note)
        {
            note = string.Empty;
            if (!policy.Enabled)
            {
                note = Bs.T("Sigurnosne kopije su isključene u postavkama projekta.");
                return null;
            }

            string file = db.Filename;
            if (string.IsNullOrEmpty(file) || !File.Exists(file) || !db.Filename.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
            {
                note = Bs.T("Crtež još nije sačuvan kao DWG, pa sigurnosna kopija nije napravljena; izmjena se može poništiti naredbom UNDO.");
                return null;
            }

            try
            {
                string? path = new BackupManager(policy).BackupFile(file, operation);
                note = Bs.T("Sigurnosna kopija posljednje sačuvane verzije: {0}", path ?? string.Empty);
                if (Convert.ToInt32(Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("DBMOD"), System.Globalization.CultureInfo.InvariantCulture) != 0)
                {
                    note += Bs.T(" (nesačuvane izmjene nisu u kopiji; zaštićene su naredbom UNDO)");
                }

                return path;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Warn(Bs.T("Sigurnosna kopija nije napravljena: {0}", ex.Message));
                note = Bs.T("Sigurnosna kopija nije napravljena: {0}", ex.Message);
                return null;
            }
        }

        public static string Begin(string operation, Database db, string? backup)
        {
            try
            {
                return Journal.Begin(operation, string.IsNullOrEmpty(db.Filename) ? "(nesačuvan crtež)" : db.Filename, backup);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Warn("Dnevnik operacija nije dostupan: " + ex.Message);
                return string.Empty;
            }
        }

        public static void End(string id, JournalState state, string message = "")
        {
            if (id.Length == 0)
            {
                return;
            }

            try
            {
                Journal.End(id, state, message);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Warn("Dnevnik operacija nije dostupan: " + ex.Message);
            }
        }

        public static List<JournalEntry> Unfinished()
        {
            try
            {
                return Journal.Unfinished();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Warn("Dnevnik operacija nije dostupan: " + ex.Message);
                return new List<JournalEntry>();
            }
        }

        /// <summary>Default policy for drawings that have no project settings yet.</summary>
        public static BackupPolicy PolicyFor(ProjectSettings? settings) => settings?.Backups ?? new BackupPolicy();
    }
}
