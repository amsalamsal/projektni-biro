using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Catalog;

namespace SiteDesignPro.AutoCAD.Drawing
{
    /// <summary>Creates, repairs and audits the project layer standard (catalog layers.json + user overrides).</summary>
    internal sealed class LayerManager
    {
        private readonly Database db;
        private readonly Transaction tr;
        private readonly ElementCatalog catalog;
        private readonly Dictionary<string, ObjectId> cache = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);

        public LayerManager(Database db, Transaction tr, ElementCatalog catalog)
        {
            this.db = db;
            this.tr = tr;
            this.catalog = catalog;
        }

        /// <summary>Layer name for a catalog layer key (falls back to the key itself).</summary>
        public string NameFor(string key) => catalog.FindLayer(key)?.Name ?? key;

        /// <summary>Ensures the layer for a catalog key exists and returns its name.</summary>
        public string Ensure(string key)
        {
            LayerDefinition? def = catalog.FindLayer(key);
            if (def == null)
            {
                Ensure(new LayerDefinition { Key = key, Name = key });
                return key;
            }

            Ensure(def);
            return def.Name;
        }

        public ObjectId Ensure(LayerDefinition def, bool repair = false)
        {
            if (!repair && cache.TryGetValue(def.Name, out ObjectId cached))
            {
                return cached;
            }

            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            LayerTableRecord ltr;
            bool created = false;
            if (table.Has(def.Name))
            {
                ltr = (LayerTableRecord)tr.GetObject(table[def.Name], repair ? OpenMode.ForWrite : OpenMode.ForRead);
            }
            else
            {
                table.UpgradeOpen();
                ltr = new LayerTableRecord { Name = def.Name };
                table.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
                created = true;
            }

            if (created || repair)
            {
                ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, def.Color);
                ltr.LinetypeObjectId = Linetype(def.Linetype);
                ltr.LineWeight = ToLineWeight(def.Lineweight);
                ltr.IsPlottable = def.Plot;
                ltr.Description = def.Description;
                if (def.Transparency > 0)
                {
                    ltr.Transparency = new Transparency((byte)Math.Round(255 * (100 - Math.Min(90, def.Transparency)) / 100.0));
                }
            }

            cache[def.Name] = ltr.ObjectId;
            return ltr.ObjectId;
        }

        /// <summary>Standard layers of version 0.1 (English names) present in the drawing, with their new names.</summary>
        public List<(string Old, string New)> LegacyLayers()
        {
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return catalog.Layers.Where(d => d.LegacyName.Length > 0 && d.LegacyName != d.Name && table.Has(d.LegacyName) && !table.Has(d.Name))
                .Select(d => (d.LegacyName, d.Name)).ToList();
        }

        /// <summary>Renames the given 0.1 layers (only on the user's explicit request; objects stay on their layer).</summary>
        public int RenameLegacy(IEnumerable<(string Old, string New)> pairs)
        {
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            int n = 0;
            foreach ((string oldName, string newName) in pairs)
            {
                if (table.Has(oldName) && !table.Has(newName))
                {
                    var ltr = (LayerTableRecord)tr.GetObject(table[oldName], OpenMode.ForWrite);
                    ltr.Name = newName;
                    n++;
                }
            }

            cache.Clear();
            return n;
        }

        /// <summary>Creates every catalog layer; with repair, resets properties to the standard. Returns (created, repaired).</summary>
        public (int Created, int Repaired) EnsureAll(bool repair)
        {
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            int created = 0, repaired = 0;
            foreach (LayerDefinition def in catalog.Layers)
            {
                bool exists = table.Has(def.Name);
                Ensure(def, repair && exists);
                if (!exists)
                {
                    created++;
                }
                else if (repair)
                {
                    repaired++;
                }
            }

            return (created, repaired);
        }

        /// <summary>Lists differences between the drawing and the layer standard.</summary>
        public List<string> Audit()
        {
            var issues = new List<string>();
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (LayerDefinition def in catalog.Layers)
            {
                if (!table.Has(def.Name))
                {
                    issues.Add(def.Name + ": missing");
                    continue;
                }

                var ltr = (LayerTableRecord)tr.GetObject(table[def.Name], OpenMode.ForRead);
                if (ltr.Color.ColorIndex != def.Color)
                {
                    issues.Add(def.Name + ": boja " + ltr.Color.ColorIndex + " (standard " + def.Color + ")");
                }

                var lt = (LinetypeTableRecord)tr.GetObject(ltr.LinetypeObjectId, OpenMode.ForRead);
                if (!string.Equals(lt.Name, def.Linetype, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(def.Name + ": tip linije " + lt.Name + " (standard " + def.Linetype + ")");
                }

                if (ltr.IsPlottable != def.Plot)
                {
                    issues.Add(def.Name + ": štampanje " + (ltr.IsPlottable ? "da" : "ne") + " (standard " + (def.Plot ? "da" : "ne") + ")");
                }
            }

            return issues;
        }

        /// <summary>Returns the linetype id, loading it from the standard linetype files if needed (Continuous on failure).</summary>
        public ObjectId Linetype(string name)
        {
            var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (lt.Has(name))
            {
                return lt[name];
            }

            foreach (string file in new[] { db.Measurement == MeasurementValue.Metric ? "acadiso.lin" : "acad.lin", "acadiso.lin", "acad.lin" })
            {
                try
                {
                    db.LoadLineTypeFile(name, file);
                    lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                    if (lt.Has(name))
                    {
                        return lt[name];
                    }
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    Log.Info("Linetype " + name + " not loaded from " + file + ": " + ex.Message);
                }
                catch (FileNotFoundException)
                {
                }
            }

            Log.Warn("Tip linije '" + name + "' nije dostupan; koristi se Continuous.");
            return lt["Continuous"];
        }

        public static LineWeight ToLineWeight(int hundredthsMm)
        {
            if (hundredthsMm < 0)
            {
                return LineWeight.ByLineWeightDefault;
            }

            LineWeight[] all = (LineWeight[])Enum.GetValues(typeof(LineWeight));
            return all.Where(w => (int)w >= 0).OrderBy(w => Math.Abs((int)w - hundredthsMm)).First();
        }
    }
}
