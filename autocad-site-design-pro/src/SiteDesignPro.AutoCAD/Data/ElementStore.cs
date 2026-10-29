using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.AutoCAD.Data
{
    /// <summary>Element data stored on a drawing entity (geometry lives in the entity itself).</summary>
    internal sealed class ElementRecord
    {
        public string Id { get; set; } = string.Empty;

        public string TypeId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Label { get; set; }

        public int Revision { get; set; } = 1;

        public ParameterSet Parameters { get; set; } = new ParameterSet();

        public SortedDictionary<string, string> Properties { get; set; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public ParameterSet Custom { get; set; } = new ParameterSet();

        /// <summary>Handle of the entity that owns this record when it was written. A different handle means the entity is a copy.</summary>
        public string OwnerHandle { get; set; } = string.Empty;

        /// <summary>Content hash at the last export (revision tracking).</summary>
        public string? LastExportHash { get; set; }

        public string? AssetId { get; set; }
    }

    /// <summary>
    /// Persists Site Design Pro data on DWG entities:
    ///  * XData (application SITEDESIGNPRO): element id and type - small, fast to filter with selection sets;
    ///  * extension dictionary Xrecord SDP_ELEMENT: full element data as JSON split into text chunks.
    /// Auxiliary entities (hatches, labels, opening boundaries) carry XData application SITEDESIGNPRO_AUX with
    /// the owner element id and their role.
    /// </summary>
    internal static class ElementStore
    {
        public const string App = "SITEDESIGNPRO";
        public const string AuxApp = "SITEDESIGNPRO_AUX";
        public const string RecordKey = "SDP_ELEMENT";
        private const int ChunkSize = 240;

        public static void EnsureRegApps(Database db, Transaction tr)
        {
            var table = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            foreach (string name in new[] { App, AuxApp })
            {
                if (!table.Has(name))
                {
                    table.UpgradeOpen();
                    var rec = new RegAppTableRecord { Name = name };
                    table.Add(rec);
                    tr.AddNewlyCreatedDBObject(rec, true);
                }
            }
        }

        public static bool IsManaged(Entity e) => e.GetXDataForApplication(App) != null;

        /// <summary>Returns (id, type) from XData without opening the extension dictionary.</summary>
        public static (string Id, string Type)? ReadKey(Entity e)
        {
            using ResultBuffer? rb = e.GetXDataForApplication(App);
            if (rb == null)
            {
                return null;
            }

            TypedValue[] v = rb.AsArray();
            return v.Length >= 3 ? ((string)v[1].Value, (string)v[2].Value) : null;
        }

        public static ElementRecord? Read(Entity e, Transaction tr)
        {
            if (e.ExtensionDictionary.IsNull)
            {
                return null;
            }

            var dict = (DBDictionary)tr.GetObject(e.ExtensionDictionary, OpenMode.ForRead);
            if (!dict.Contains(RecordKey))
            {
                return null;
            }

            var xr = (Xrecord)tr.GetObject(dict.GetAt(RecordKey), OpenMode.ForRead);
            using ResultBuffer? data = xr.Data;
            if (data == null)
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (TypedValue tv in data)
            {
                if (tv.TypeCode == (short)DxfCode.Text)
                {
                    sb.Append((string)tv.Value);
                }
            }

            return FromJson(sb.ToString());
        }

        public static void Write(Entity e, ElementRecord record, Transaction tr)
        {
            record.OwnerHandle = e.Handle.ToString();
            EnsureRegApps(e.Database, tr);
            if (!e.IsWriteEnabled)
            {
                e.UpgradeOpen();
            }

            e.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, App),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, record.Id),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, record.TypeId));

            if (e.ExtensionDictionary.IsNull)
            {
                e.CreateExtensionDictionary();
            }

            var dict = (DBDictionary)tr.GetObject(e.ExtensionDictionary, OpenMode.ForWrite);
            string json = ToJson(record);
            var rb = new ResultBuffer(new TypedValue((int)DxfCode.Int16, 1));
            for (int i = 0; i < json.Length; i += ChunkSize)
            {
                rb.Add(new TypedValue((int)DxfCode.Text, json.Substring(i, Math.Min(ChunkSize, json.Length - i))));
            }

            if (dict.Contains(RecordKey))
            {
                var xr = (Xrecord)tr.GetObject(dict.GetAt(RecordKey), OpenMode.ForWrite);
                xr.Data = rb;
            }
            else
            {
                var xr = new Xrecord { Data = rb };
                dict.SetAt(RecordKey, xr);
                tr.AddNewlyCreatedDBObject(xr, true);
            }
        }

        /// <summary>Removes Site Design Pro data from an entity (it becomes ordinary drawing geometry).</summary>
        public static void Detach(Entity e, Transaction tr)
        {
            if (!e.IsWriteEnabled)
            {
                e.UpgradeOpen();
            }

            e.XData = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, App));
            if (!e.ExtensionDictionary.IsNull)
            {
                var dict = (DBDictionary)tr.GetObject(e.ExtensionDictionary, OpenMode.ForWrite);
                if (dict.Contains(RecordKey))
                {
                    tr.GetObject(dict.GetAt(RecordKey), OpenMode.ForWrite).Erase();
                }
            }
        }

        public static void WriteAux(Entity e, string ownerId, string role, Transaction tr)
        {
            EnsureRegApps(e.Database, tr);
            if (!e.IsWriteEnabled)
            {
                e.UpgradeOpen();
            }

            e.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, AuxApp),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, ownerId),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, role));
        }

        public static (string OwnerId, string Role)? ReadAux(Entity e)
        {
            using ResultBuffer? rb = e.GetXDataForApplication(AuxApp);
            if (rb == null)
            {
                return null;
            }

            TypedValue[] v = rb.AsArray();
            return v.Length >= 3 ? ((string)v[1].Value, (string)v[2].Value) : null;
        }

        public static string ToJson(ElementRecord r)
        {
            var o = new JsonObject
            {
                ["v"] = 1,
                ["id"] = r.Id,
                ["type"] = r.TypeId,
                ["name"] = r.Name,
                ["label"] = r.Label,
                ["revision"] = r.Revision,
                ["params"] = r.Parameters.ToJson(),
                ["props"] = new JsonObject(r.Properties.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, kv.Value))),
                ["custom"] = r.Custom.ToJson(),
                ["handle"] = r.OwnerHandle,
                ["lastExportHash"] = r.LastExportHash,
                ["asset"] = r.AssetId,
            };
            return o.ToJsonString();
        }

        public static ElementRecord FromJson(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement r = doc.RootElement;
            string Str(string name) => r.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
            string? Opt(string name) => r.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var rec = new ElementRecord
            {
                Id = Str("id"),
                TypeId = Str("type"),
                Name = Str("name"),
                Label = Opt("label"),
                Revision = r.TryGetProperty("revision", out JsonElement rev) && rev.ValueKind == JsonValueKind.Number ? rev.GetInt32() : 1,
                Parameters = r.TryGetProperty("params", out JsonElement p) ? ParameterSet.FromJson(p) : new ParameterSet(),
                Custom = r.TryGetProperty("custom", out JsonElement c) ? ParameterSet.FromJson(c) : new ParameterSet(),
                OwnerHandle = Str("handle"),
                LastExportHash = Opt("lastExportHash"),
                AssetId = Opt("asset"),
            };
            if (r.TryGetProperty("props", out JsonElement props) && props.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty prop in props.EnumerateObject())
                {
                    rec.Properties[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }

            return rec;
        }
    }

    /// <summary>Project settings stored in the drawing's named objects dictionary (SDP_PROJECT Xrecord).</summary>
    internal static class ProjectStore
    {
        private const string Key = "SDP_PROJECT";
        private const int ChunkSize = 240;

        public static ProjectSettings? Read(Database db, Transaction tr)
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(Key))
            {
                return null;
            }

            var xr = (Xrecord)tr.GetObject(nod.GetAt(Key), OpenMode.ForRead);
            using ResultBuffer? data = xr.Data;
            if (data == null)
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (TypedValue tv in data)
            {
                if (tv.TypeCode == (short)DxfCode.Text)
                {
                    sb.Append((string)tv.Value);
                }
            }

            return ProjectSettings.FromJson(sb.ToString());
        }

        public static void Write(Database db, Transaction tr, ProjectSettings settings)
        {
            string json = settings.ToJson();
            var rb = new ResultBuffer(new TypedValue((int)DxfCode.Int16, settings.SettingsVersion));
            for (int i = 0; i < json.Length; i += ChunkSize)
            {
                rb.Add(new TypedValue((int)DxfCode.Text, json.Substring(i, Math.Min(ChunkSize, json.Length - i))));
            }

            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
            if (nod.Contains(Key))
            {
                ((Xrecord)tr.GetObject(nod.GetAt(Key), OpenMode.ForWrite)).Data = rb;
            }
            else
            {
                var xr = new Xrecord { Data = rb };
                nod.SetAt(Key, xr);
                tr.AddNewlyCreatedDBObject(xr, true);
            }
        }

        /// <summary>Settings of the drawing, or defaults derived from INSUNITS when the drawing has no project yet.</summary>
        public static ProjectSettings ReadOrDefault(Database db, Transaction tr)
        {
            ProjectSettings? s = Read(db, tr);
            if (s != null)
            {
                return s;
            }

            Core.Units.LengthUnit? unit = Core.Units.UnitConversion.FromInsUnits((int)db.Insunits);
            return new ProjectSettings
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(db.Filename) is string n && n.Length > 0 ? n : "Untitled site project",
                DrawingUnit = Core.Units.UnitConversion.Code(unit ?? Core.Units.LengthUnit.Millimeter),
            };
        }
    }
}
