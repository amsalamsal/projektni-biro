using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.Core.Safety
{
    /// <summary>Atomic file writes: write a temporary file next to the target, verify it, then swap.</summary>
    public static class SafeFile
    {
        public static void WriteAllTextAtomic(string path, string text) => WriteAllBytesAtomic(path, new UTF8Encoding(false).GetBytes(text));

        public static void WriteAllBytesAtomic(string path, byte[] data)
        {
            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full) ?? ".";
            Directory.CreateDirectory(dir);
            string temp = Path.Combine(dir, "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp");
            try
            {
                using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    fs.Write(data, 0, data.Length);
                    fs.Flush(true);
                }

                if (new FileInfo(temp).Length != data.Length)
                {
                    throw new IOException(Bs.T("Privremena datoteka {0} nije potpuno zapisana.", temp));
                }

                Compat.ReplaceFile(temp, full);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
        }
    }

    /// <summary>Backup retention rules (user-configurable).</summary>
    public sealed class BackupPolicy
    {
        public bool Enabled { get; set; } = true;

        /// <summary>Backups kept per source file; older ones are removed (never the newest).</summary>
        public int KeepPerFile { get; set; } = 10;

        /// <summary>Backups older than this many days are removed (0 = keep regardless of age); the newest always stays.</summary>
        public int MaxAgeDays { get; set; } = 30;

        /// <summary>Backup folder; empty = "&lt;source folder&gt;\_SDP_backup".</summary>
        public string Folder { get; set; } = string.Empty;
    }

    public sealed class BackupInfo
    {
        public string Path { get; set; } = string.Empty;

        public string SourceName { get; set; } = string.Empty;

        public DateTime CreatedUtc { get; set; }

        public string Operation { get; set; } = string.Empty;

        public long Size { get; set; }
    }

    /// <summary>
    /// Timestamped backups "name_YYYYMMDD-HHMMSS_operation.ext". A backup is verified (size and SHA-256) before it
    /// counts; pruning never deletes the newest backup of a file and never touches the source.
    /// </summary>
    public sealed class BackupManager
    {
        public const string DefaultFolderName = "_SDP_backup";

        private readonly Func<DateTime> clock;

        public BackupManager(BackupPolicy policy, Func<DateTime>? utcClock = null)
        {
            Policy = policy;
            clock = utcClock ?? (() => DateTime.UtcNow);
        }

        public BackupPolicy Policy { get; }

        public string FolderFor(string sourcePath) =>
            string.IsNullOrWhiteSpace(Policy.Folder) ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? ".", DefaultFolderName) : Policy.Folder;

        public static string Slug(string operation)
        {
            var sb = new StringBuilder();
            foreach (char c in operation.ToLowerInvariant())
            {
                sb.Append(char.IsLetterOrDigit(c) && c < 128 ? c : '-');
            }

            string s = sb.ToString().Trim('-');
            while (s.Contains("--"))
            {
                s = s.Replace("--", "-");
            }

            return s.Length == 0 ? "operacija" : (s.Length > 40 ? s.Substring(0, 40) : s);
        }

        /// <summary>Path a backup of <paramref name="sourcePath"/> for <paramref name="operation"/> would get now.</summary>
        public string NextBackupPath(string sourcePath, string operation)
        {
            string stamp = clock().ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string name = Path.GetFileNameWithoutExtension(sourcePath) + "_" + stamp + "_" + Slug(operation) + Path.GetExtension(sourcePath);
            string path = Path.Combine(FolderFor(sourcePath), name);
            int n = 2;
            while (File.Exists(path))
            {
                path = Path.Combine(FolderFor(sourcePath), Path.GetFileNameWithoutExtension(name) + "_" + n++ + Path.GetExtension(sourcePath));
            }

            return path;
        }

        /// <summary>Copies an existing file to a verified backup. Returns the backup path, or null when backups are disabled.</summary>
        public string? BackupFile(string sourcePath, string operation)
        {
            if (!Policy.Enabled)
            {
                return null;
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(Bs.T("Datoteka za sigurnosnu kopiju ne postoji: {0}", sourcePath), sourcePath);
            }

            string target = NextBackupPath(sourcePath, operation);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(sourcePath, target, false);
            Verify(sourcePath, target);
            Prune(sourcePath);
            return target;
        }

        /// <summary>Registers a backup written by the host (e.g. a database copy) and applies retention.</summary>
        public void Adopt(string sourcePath, string backupPath)
        {
            if (!File.Exists(backupPath) || new FileInfo(backupPath).Length == 0)
            {
                throw new IOException(Bs.T("Sigurnosna kopija {0} nije ispravno zapisana.", backupPath));
            }

            Prune(sourcePath);
        }

        /// <summary>
        /// Reads a file that another program may hold open for writing (AutoCAD keeps the open DWG locked that way).
        /// File.ReadAllBytes asks for exclusive write access and fails with a sharing violation.
        /// </summary>
        private static byte[] ReadShared(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static void Verify(string a, string b)
        {
            byte[] ha = Compat.Sha256(ReadShared(a));
            byte[] hb = Compat.Sha256(ReadShared(b));
            if (!ha.SequenceEqual(hb))
            {
                File.Delete(b);
                throw new IOException(Bs.T("Provjera sigurnosne kopije nije uspjela (sadržaj se razlikuje): {0}", b));
            }
        }

        public List<BackupInfo> List(string sourcePath)
        {
            string folder = FolderFor(sourcePath);
            string stem = Path.GetFileNameWithoutExtension(sourcePath) + "_";
            string ext = Path.GetExtension(sourcePath);
            if (!Directory.Exists(folder))
            {
                return new List<BackupInfo>();
            }

            var list = new List<BackupInfo>();
            foreach (string f in Directory.GetFiles(folder, stem + "*" + ext))
            {
                string rest = Path.GetFileNameWithoutExtension(f).Substring(stem.Length);
                string[] parts = rest.Split('_');
                if (parts.Length < 2 || !DateTime.TryParseExact(parts[0], "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime local))
                {
                    continue; // not one of ours
                }

                list.Add(new BackupInfo { Path = f, SourceName = Path.GetFileName(sourcePath), CreatedUtc = local.ToUniversalTime(), Operation = parts[1], Size = new FileInfo(f).Length });
            }

            return list.OrderByDescending(b => b.CreatedUtc).ThenByDescending(b => b.Path, StringComparer.Ordinal).ToList();
        }

        /// <summary>Applies retention; the newest backup always survives. Returns deleted paths.</summary>
        public List<string> Prune(string sourcePath)
        {
            var deleted = new List<string>();
            List<BackupInfo> all = List(sourcePath);
            DateTime now = clock();
            for (int i = 1; i < all.Count; i++)
            {
                bool tooMany = Policy.KeepPerFile > 0 && i >= Policy.KeepPerFile;
                bool tooOld = Policy.MaxAgeDays > 0 && (now - all[i].CreatedUtc).TotalDays > Policy.MaxAgeDays;
                if (tooMany || tooOld)
                {
                    try
                    {
                        File.Delete(all[i].Path);
                        deleted.Add(all[i].Path);
                    }
                    catch (IOException)
                    {
                        // locked backups are left for the next run
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }

            return deleted;
        }

        /// <summary>Restores a backup into a NEW file next to the source (never overwrites the source).</summary>
        public string RestoreAsCopy(BackupInfo backup, string sourcePath)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? ".";
            string target = Path.Combine(dir, Path.GetFileNameWithoutExtension(sourcePath) + "_vraceno_" + Path.GetFileNameWithoutExtension(backup.Path).Substring(Path.GetFileNameWithoutExtension(sourcePath).Length + 1) + Path.GetExtension(sourcePath));
            if (File.Exists(target))
            {
                throw new IOException(Bs.T("Datoteka {0} već postoji; vraćanje ne prepisuje postojeće datoteke.", target));
            }

            File.Copy(backup.Path, target, false);
            return target;
        }
    }

    public enum JournalState
    {
        Started,
        Completed,
        Failed,
        RolledBack,
    }

    public sealed class JournalEntry
    {
        public string OperationId { get; set; } = string.Empty;

        public string Operation { get; set; } = string.Empty;

        public string Document { get; set; } = string.Empty;

        public string? BackupPath { get; set; }

        public JournalState State { get; set; }

        public string Utc { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Append-only recovery journal (JSON lines). Each major operation writes "Started" before it changes anything
    /// and "Completed"/"Failed"/"RolledBack" afterwards. An operation without an end record after a crash is reported
    /// at the next start together with its backup.
    /// </summary>
    public sealed class RecoveryJournal
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        private readonly object gate = new object();
        private readonly Func<DateTime> clock;

        public RecoveryJournal(string path, Func<DateTime>? utcClock = null)
        {
            Path = path;
            clock = utcClock ?? (() => DateTime.UtcNow);
        }

        public string Path { get; }

        public string Begin(string operation, string document, string? backupPath)
        {
            string id = Guid.NewGuid().ToString("N");
            Append(new JournalEntry { OperationId = id, Operation = operation, Document = document, BackupPath = backupPath, State = JournalState.Started });
            return id;
        }

        public void End(string operationId, JournalState state, string message = "")
        {
            JournalEntry? start = Read().FirstOrDefault(e => e.OperationId == operationId && e.State == JournalState.Started);
            Append(new JournalEntry
            {
                OperationId = operationId,
                Operation = start?.Operation ?? string.Empty,
                Document = start?.Document ?? string.Empty,
                BackupPath = start?.BackupPath,
                State = state,
                Message = message,
            });
        }

        private void Append(JournalEntry e)
        {
            e.Utc = clock().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            string line = JsonSerializer.Serialize(e, Options) + "\n";
            lock (gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
                using (var fs = new FileStream(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                {
                    // a crash may have left a torn last line without a newline: start a fresh line
                    if (fs.Length > 0)
                    {
                        fs.Seek(-1, SeekOrigin.End);
                        if (fs.ReadByte() != '\n')
                        {
                            line = "\n" + line;
                        }
                    }

                    fs.Seek(0, SeekOrigin.End);
                    byte[] b = new UTF8Encoding(false).GetBytes(line);
                    fs.Write(b, 0, b.Length);
                    fs.Flush(true);
                }
            }
        }

        /// <summary>All readable entries; a torn last line (crash during write) is skipped.</summary>
        public List<JournalEntry> Read()
        {
            var list = new List<JournalEntry>();
            if (!File.Exists(Path))
            {
                return list;
            }

            string[] lines;
            lock (gate)
            {
                using (var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    lines = sr.ReadToEnd().Split('\n');
                }
            }

            foreach (string l in lines)
            {
                if (string.IsNullOrWhiteSpace(l))
                {
                    continue;
                }

                try
                {
                    JournalEntry? e = JsonSerializer.Deserialize<JournalEntry>(l, Options);
                    if (e != null)
                    {
                        list.Add(e);
                    }
                }
                catch (JsonException)
                {
                    // incomplete line from an interrupted write
                }
            }

            return list;
        }

        /// <summary>Operations that started but never finished (interrupted), optionally for one document.</summary>
        public List<JournalEntry> Unfinished(string? document = null)
        {
            List<JournalEntry> all = Read();
            var ended = new HashSet<string>(all.Where(e => e.State != JournalState.Started).Select(e => e.OperationId));
            return all.Where(e => e.State == JournalState.Started && !ended.Contains(e.OperationId)
                && (document == null || string.Equals(e.Document, document, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        /// <summary>Marks an interrupted operation as handled by the user.</summary>
        public void Acknowledge(string operationId, string message) => End(operationId, JournalState.RolledBack, message);

        /// <summary>Keeps the journal small: retains the last <paramref name="keep"/> entries plus all unfinished operations.</summary>
        public void Compact(int keep = 500)
        {
            List<JournalEntry> all = Read();
            if (all.Count <= keep)
            {
                return;
            }

            var unfinished = new HashSet<string>(Unfinished().Select(u => u.OperationId));
            List<JournalEntry> kept = all.Where((e, i) => i >= all.Count - keep || unfinished.Contains(e.OperationId)).ToList();
            SafeFile.WriteAllTextAtomic(Path, string.Concat(kept.Select(e => JsonSerializer.Serialize(e, Options) + "\n")));
        }

        public static string Describe(JournalEntry e) =>
            Bs.T("{0} | {1} | {2}{3}", e.Utc, e.Operation, e.Document, string.IsNullOrEmpty(e.BackupPath) ? string.Empty : " | " + Bs.T("kopija: ") + e.BackupPath);
    }
}
