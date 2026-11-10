using System;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;

namespace SiteDesignPro.AutoCAD.Data
{
    /// <summary>
    /// Base elevation of the roads of a drawing (m above sea level) and the terrain mode: flat (all roads at the
    /// base elevation) or uneven (heights from the elevation marks, relative to the base). Stored in the drawing.
    /// </summary>
    internal sealed class RoadLevelSettings
    {
        private const string Key = "SDP_ROAD_LEVELS";

        public bool Flat { get; set; } = true;

        /// <summary>Base elevation of the roads, m above sea level.</summary>
        public double Base { get; set; }

        public bool Defined { get; set; }

        public static RoadLevelSettings Read(Database db, Transaction tr)
        {
            var s = new RoadLevelSettings();
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(Key))
            {
                return s;
            }

            var xr = (Xrecord)tr.GetObject(nod.GetAt(Key), OpenMode.ForRead);
            using ResultBuffer? rb = xr.Data;
            if (rb == null)
            {
                return s;
            }

            foreach (TypedValue v in rb)
            {
                if (v.TypeCode == (int)DxfCode.Text)
                {
                    s.Flat = string.Equals(Convert.ToString(v.Value, CultureInfo.InvariantCulture), "flat", StringComparison.Ordinal);
                }
                else if (v.TypeCode == (int)DxfCode.Real)
                {
                    s.Base = Convert.ToDouble(v.Value, CultureInfo.InvariantCulture);
                }
            }

            s.Defined = true;
            return s;
        }

        public void Write(Database db, Transaction tr)
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
            Xrecord xr;
            if (nod.Contains(Key))
            {
                xr = (Xrecord)tr.GetObject(nod.GetAt(Key), OpenMode.ForWrite);
            }
            else
            {
                xr = new Xrecord();
                nod.SetAt(Key, xr);
                tr.AddNewlyCreatedDBObject(xr, true);
            }

            xr.Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, Flat ? "flat" : "uneven"), new TypedValue((int)DxfCode.Real, Base));
        }
    }
}
