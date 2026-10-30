using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Labels;
using SiteDesignPro.Core.Model;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>Thrown to end a command quietly after the user cancels.</summary>
    internal sealed class CommandCancelled : Exception
    {
    }

    /// <summary>
    /// Per-command services: document, editor, one transaction, catalog, project settings and the element service.
    /// All drawing modifications of a command happen in this transaction (one UNDO step); exceptions abort it.
    /// </summary>
    internal sealed class CommandContext : IDisposable
    {
        private bool committed;

        private CommandContext(Document doc)
        {
            Doc = doc;
            Db = doc.Database;
            Ed = doc.Editor;
            Catalog = AcadEnv.Catalog;
            Tr = Db.TransactionManager.StartTransaction();
            Settings = ProjectStore.ReadOrDefault(Db, Tr);
            Svc = new ElementService(Db, Tr, Catalog, Settings);
        }

        public Document Doc { get; }

        public Database Db { get; }

        public Editor Ed { get; }

        public Transaction Tr { get; }

        public ElementCatalog Catalog { get; }

        public ProjectSettings Settings { get; }

        public ElementService Svc { get; }

        public double K => Settings.Scale.DrawingUnitsPerMeter;

        /// <summary>Runs a command body with error handling, logging and transaction management.</summary>
        public static void Run(string name, Action<CommandContext> body) => Run(name, null, body);

        /// <summary>
        /// Runs a major operation (bulk placement, import, terrain, synchronisation): the saved drawing is backed up
        /// first, the operation is journalled, and everything happens in one transaction (one UNDO step); on any
        /// error the transaction is aborted so no half-created geometry remains.
        /// </summary>
        public static void RunMajor(string name, string operation, Action<CommandContext> body) => Run(name, operation, body);

        private static void Run(string name, string? majorOperation, Action<CommandContext> body)
        {
            Document? doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            string journalId = string.Empty;
            try
            {
                using var ctx = new CommandContext(doc);
                if (majorOperation != null)
                {
                    string? backup = SafetyHost.BackupDrawing(doc.Database, ctx.Settings.Backups, majorOperation, out string note);
                    if (note.Length > 0)
                    {
                        ctx.Write(note);
                    }

                    journalId = SafetyHost.Begin(majorOperation, doc.Database, backup);
                }

                body(ctx);
                ctx.Commit();
                SafetyHost.End(journalId, Core.Safety.JournalState.Completed);
            }
            catch (CommandCancelled)
            {
                SafetyHost.End(journalId, Core.Safety.JournalState.RolledBack, "Prekinuo korisnik.");
                doc.Editor.WriteMessage("\n*Prekinuto*");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                SafetyHost.End(journalId, Core.Safety.JournalState.RolledBack, ex.Message);
                Log.Error(name + " nije uspjela (AutoCAD " + ex.ErrorStatus + "); izmjene su poništene", ex);
            }
            catch (Exception ex)
            {
                // Every other error is reported too: an exception escaping a command shows AutoCAD's crash dialog.
                SafetyHost.End(journalId, Core.Safety.JournalState.RolledBack, ex.Message);
                Log.Error(name + " nije uspjela; izmjene su poništene", ex);
            }
        }

        /// <summary>Runs a command body that needs no transaction (opening files, palettes) with the same error reporting.</summary>
        public static void Safe(string name, Action body)
        {
            try
            {
                body();
            }
            catch (CommandCancelled)
            {
                AcadEnv.Message("*Prekinuto*");
            }
            catch (Exception ex)
            {
                Log.Error(name + " nije uspjela", ex);
            }
        }

        public void Commit()
        {
            if (!committed)
            {
                Tr.Commit();
                committed = true;
            }
        }

        public void SaveSettings()
        {
            ProjectStore.Write(Db, Tr, Settings);
        }

        public void Dispose()
        {
            Tr.Dispose();
        }

        public void Write(string text) => Ed.WriteMessage("\n" + text);

        // ------------------------------------------------------------------ prompts

        /// <summary>
        /// Keyword prompt. A keyword may be given as "Global|Lokalni": the Bosnian local name is shown and typed,
        /// the global name is returned (so code can compare stable names). Without '|' both names are the same.
        /// </summary>
        public string Keyword(string message, string[] keywords, string? defaultKeyword = null)
        {
            var o = new PromptKeywordOptions("\n" + message);
            foreach (string k in keywords)
            {
                int bar = k.IndexOf('|');
                if (bar > 0)
                {
                    o.Keywords.Add(k.Substring(0, bar), k.Substring(bar + 1), k.Substring(bar + 1));
                }
                else
                {
                    o.Keywords.Add(k);
                }
            }

            if (defaultKeyword != null)
            {
                o.Keywords.Default = defaultKeyword;
            }

            // The messages already list the keywords in [...]; AutoCAD must not append them a second time.
            if (message.Contains("["))
            {
                o.AppendKeywordsToMessage = false;
                if (defaultKeyword != null)
                {
                    string shown = keywords.Select(k => k.Split('|')).Where(p => p[0] == defaultKeyword).Select(p => p[p.Length - 1]).FirstOrDefault() ?? defaultKeyword;
                    o.Message = "\n" + message + " <" + shown + ">";
                }
            }

            o.AllowNone = defaultKeyword != null;
            PromptResult r = Ed.GetKeywords(o);
            if (r.Status == PromptStatus.None && defaultKeyword != null)
            {
                return defaultKeyword;
            }

            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            return r.StringResult;
        }

        public string Text(string message, string? defaultValue = null, bool allowSpaces = true)
        {
            var o = new PromptStringOptions("\n" + message + (defaultValue != null ? " <" + defaultValue + ">" : string.Empty) + ": ") { AllowSpaces = allowSpaces };
            PromptResult r = Ed.GetString(o);
            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            return r.StringResult.Length == 0 && defaultValue != null ? defaultValue : r.StringResult;
        }

        public double Number(string message, double defaultValue, bool allowNegative = false, bool allowZero = false)
        {
            var o = new PromptDoubleOptions("\n" + message) { DefaultValue = defaultValue, UseDefaultValue = true, AllowNegative = allowNegative, AllowZero = allowZero };
            PromptDoubleResult r = Ed.GetDouble(o);
            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            return r.Value;
        }

        public int Integer(string message, int defaultValue, int min = 1)
        {
            var o = new PromptIntegerOptions("\n" + message) { DefaultValue = defaultValue, UseDefaultValue = true, LowerLimit = min, AllowNegative = min < 0, AllowZero = min <= 0 };
            PromptIntegerResult r = Ed.GetInteger(o);
            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            return r.Value;
        }

        public Point3d? Point(string message, Point3d? basePoint = null, bool allowNone = false)
        {
            var o = new PromptPointOptions("\n" + message) { AllowNone = allowNone };
            if (basePoint.HasValue)
            {
                o.BasePoint = basePoint.Value.TransformBy(Ed.CurrentUserCoordinateSystem.Inverse());
                o.UseBasePoint = true;
            }

            PromptPointResult r = Ed.GetPoint(o);
            if (r.Status == PromptStatus.None && allowNone)
            {
                return null;
            }

            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            return r.Value.TransformBy(Ed.CurrentUserCoordinateSystem);
        }

        public double Angle(string message, Point3d basePoint, double defaultRadians)
        {
            var o = new PromptAngleOptions("\n" + message) { BasePoint = basePoint.TransformBy(Ed.CurrentUserCoordinateSystem.Inverse()), UseBasePoint = true, DefaultValue = defaultRadians, UseDefaultValue = true, AllowNone = true };
            PromptDoubleResult r = Ed.GetAngle(o);
            if (r.Status == PromptStatus.None)
            {
                return defaultRadians;
            }

            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            // GetAngle returns the angle in the current UCS; convert to WCS about the UCS Z axis
            Matrix3d ucs = Ed.CurrentUserCoordinateSystem;
            Vector3d x = Vector3d.XAxis.TransformBy(ucs);
            return GeometryMath.NormalizeAngle(r.Value + Math.Atan2(x.Y, x.X));
        }

        public ObjectId Entity(string message, params Type[] allowed)
        {
            var o = new PromptEntityOptions("\n" + message);
            if (allowed.Length > 0)
            {
                o.SetRejectMessage("\nOva vrsta objekta nije podržana.");
                foreach (Type t in allowed)
                {
                    o.AddAllowedClass(t, false);
                }
            }

            PromptEntityResult r = Ed.GetEntity(o);
            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            return r.ObjectId;
        }

        /// <summary>Selects managed elements (implied selection first); "All" selects every managed element.</summary>
        public List<ManagedElement> SelectElements(string message, bool allowAll = true, Func<ManagedElement, bool>? filter = null)
        {
            var sf = new SelectionFilter(new[] { new TypedValue((int)DxfCode.ExtendedDataRegAppName, ElementStore.App) });
            PromptSelectionResult implied = Ed.SelectImplied();
            ObjectId[] ids;
            if (implied.Status == PromptStatus.OK && implied.Value.Count > 0)
            {
                ids = implied.Value.GetObjectIds();
                Ed.SetImpliedSelection(Array.Empty<ObjectId>());
            }
            else
            {
                var o = new PromptSelectionOptions { MessageForAdding = "\n" + message + (allowAll ? " (ili Enter za sve)" : string.Empty) };
                PromptSelectionResult r = Ed.GetSelection(o, sf);
                if (r.Status == PromptStatus.Error && allowAll)
                {
                    PromptSelectionResult all = Ed.SelectAll(sf);
                    ids = all.Status == PromptStatus.OK ? all.Value.GetObjectIds() : Array.Empty<ObjectId>();
                }
                else if (r.Status != PromptStatus.OK)
                {
                    throw new CommandCancelled();
                }
                else
                {
                    ids = r.Value.GetObjectIds();
                }
            }

            var result = new List<ManagedElement>();
            foreach (ObjectId id in ids)
            {
                if (Tr.GetObject(id, OpenMode.ForRead) is Entity e && ElementStore.IsManaged(e))
                {
                    ManagedElement? m = Svc.Read(e);
                    if (m != null && (filter == null || filter(m)))
                    {
                        result.Add(m);
                    }
                }
            }

            return result;
        }

        /// <summary>Asks for an element type by id or name fragment, optionally restricted to geometry kinds.</summary>
        public ElementType ChooseType(string message, string? argument, params GeometryKind[] kinds)
        {
            string query = argument ?? Text(message + " (id tipa ili dio naziva, ? za popis)", null, true);
            for (int attempt = 0; attempt < 5; attempt++)
            {
                IEnumerable<ElementType> pool = Catalog.Types.Where(t => kinds.Length == 0 || kinds.Contains(t.Geometry));
                ElementType? exact = pool.FirstOrDefault(t => string.Equals(t.Id, query, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    return exact;
                }

                List<ElementType> matches = query == "?" ? pool.ToList() : pool.Where(t => t.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || t.NameEn.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || t.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (matches.Count == 1)
                {
                    return matches[0];
                }

                if (matches.Count == 0)
                {
                    Write("Nijedan tip elementa ne odgovara upitu '" + query + "'.");
                }
                else
                {
                    foreach (ElementType t in matches.Take(40))
                    {
                        Write("  " + t.Id.PadRight(40) + t.Name);
                    }

                    if (matches.Count > 40)
                    {
                        Write("  ... još " + (matches.Count - 40) + "; suzite pretragu ili koristite paletu Vanjsko uređenje PRO.");
                    }
                }

                query = Text(message + " (id tipa)", null, true);
            }

            throw new CommandCancelled();
        }

        /// <summary>New element with type defaults, next label and the given drawing-frame geometry.</summary>
        public SiteElement NewElement(ElementType type, ElementGeometry geometry, IEnumerable<SiteElement> existing)
        {
            var e = new SiteElement(SiteElement.NewId(), type.Id, geometry) { Name = type.Name, Parameters = type.CreateDefaultParameters() };
            e.Label = new LabelNumbering(Settings.Labels, Catalog).Next(type, existing);
            return e;
        }

        /// <summary>All elements currently in the drawing (drawing frame), for numbering.</summary>
        public List<SiteElement> ExistingElements() => Svc.ReadAll().Where(m => m.Element != null && !m.IsCopy).Select(m => m.Element!).ToList();

        /// <summary>Display number with a decimal comma (Bosnian), trailing zeros removed.</summary>
        public static string F(double v, int decimals = 3) => v.ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
