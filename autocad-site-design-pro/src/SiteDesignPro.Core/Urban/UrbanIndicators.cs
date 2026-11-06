using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Quantities;

namespace SiteDesignPro.Core.Urban
{
    /// <summary>
    /// Limits from the regulation plan (regulacioni plan / urbanistička saglasnost) the design is compared with.
    /// Every value is optional: an empty requirement shows the designed value without a verdict.
    /// </summary>
    public sealed class UrbanRequirements
    {
        /// <summary>Maximum coverage ratio (koeficijent izgrađenosti), e.g. 0.40.</summary>
        public double? MaxCoverage { get; set; }

        /// <summary>Maximum floor area ratio (koeficijent iskorištenosti), e.g. 1.6.</summary>
        public double? MaxFloorAreaRatio { get; set; }

        /// <summary>Minimum share of green areas on the parcel, percent.</summary>
        public double? MinGreenPercent { get; set; }

        /// <summary>Minimum share of permeable (green and water-permeable) areas on the parcel, percent.</summary>
        public double? MinPermeablePercent { get; set; }

        /// <summary>Number of dwellings (stanova); with <see cref="ParkingPerDwelling"/> gives the parking requirement.</summary>
        public int? Dwellings { get; set; }

        /// <summary>Parking spaces required per dwelling, e.g. 1.1.</summary>
        public double? ParkingPerDwelling { get; set; }

        /// <summary>Fixed minimum number of parking spaces (used when larger than dwellings × norm).</summary>
        public int? MinParking { get; set; }

        /// <summary>Minimum share of parking spaces for persons with disabilities, percent (often 5 %).</summary>
        public double? MinAccessiblePercent { get; set; }

        /// <summary>Minimum number of parking spaces with an electric-vehicle charger.</summary>
        public int? MinEvSpaces { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsEmpty => MaxCoverage == null && MaxFloorAreaRatio == null && MinGreenPercent == null && MinPermeablePercent == null &&
            Dwellings == null && ParkingPerDwelling == null && MinParking == null && MinAccessiblePercent == null && MinEvSpaces == null;
    }

    /// <summary>One row of the indicator table.</summary>
    public sealed class UrbanIndicator
    {
        public UrbanIndicator(string name, string value, string requirement, bool? met)
        {
            Name = name;
            Value = value;
            Requirement = requirement;
            Met = met;
        }

        public string Name { get; }

        public string Value { get; }

        public string Requirement { get; }

        /// <summary>True = requirement met, false = not met, null = no requirement or not computable.</summary>
        public bool? Met { get; }

        public string Verdict => Met == null ? "–" : Met.Value ? "ispunjeno" : "NIJE ispunjeno";
    }

    /// <summary>Measured areas and counts the indicators are computed from (metres, m²).</summary>
    public sealed class UrbanMeasures
    {
        public double Parcel { get; set; }

        public double Footprint { get; set; }

        public double FloorArea { get; set; }

        public double Green { get; set; }

        public double Permeable { get; set; }

        public int Parking { get; set; }

        public int Accessible { get; set; }

        public int Ev { get; set; }

        public int Loading { get; set; }
    }

    /// <summary>
    /// Urban planning indicators of a site (coverage, floor area ratio, green and permeable share, parking) compared with
    /// the regulation plan. Elements must be in metres (package frame).
    /// </summary>
    public static class UrbanIndicators
    {
        /// <summary>Planted areas counted as green (zelene površine).</summary>
        public static bool IsGreen(ElementType t) =>
            t.Builder.Id == "planting_area" || t.Id == "surface.lawn" || t.Id == "landscape.lawn_area";

        /// <summary>Areas that let rain water through: green areas plus permeable surfaces and rain gardens.</summary>
        public static bool IsPermeable(ElementType t) =>
            IsGreen(t) || t.Id == "surface.permeable_paving" || t.Id == "surface.grass_grid" || t.Id == "surface.gravel" ||
            t.Id == "surface.sand" || t.Id == "surface.mulch" || t.Id == "water.rain_garden" || t.Id == "water.bioswale" || t.Id == "water.retention_basin";

        public static UrbanMeasures Measure(IEnumerable<SiteElement> elements, ElementCatalog catalog)
        {
            var m = new UrbanMeasures();
            foreach (SiteElement e in elements)
            {
                ElementType? t = catalog.FindType(e.TypeId);
                if (t == null)
                {
                    continue;
                }

                double area = e.Geometry is PolygonGeometry pg ? GeometryMath.Area(pg) : 0;
                if (t.Id == "site.boundary")
                {
                    m.Parcel += area;
                }
                else if (t.Id == "building.footprint")
                {
                    m.Footprint += area;
                    double storeys = e.Parameters.Number("storeys", t.Parameter("storeys")?.Default.AsNumber() ?? 1);
                    m.FloorArea += area * Math.Max(1, Compat.IsFinite(storeys) ? storeys : 1);
                }
                else if (IsGreen(t))
                {
                    m.Green += area;
                    m.Permeable += area;
                }
                else if (IsPermeable(t))
                {
                    m.Permeable += area;
                }

                switch (t.Id)
                {
                    case "road.parking_bay":
                        m.Parking++;
                        break;
                    case "road.parking_bay_accessible":
                        m.Parking++;
                        m.Accessible++;
                        break;
                    case "road.parking_bay_ev":
                        m.Parking++;
                        m.Ev++;
                        break;
                    case "road.loading_bay":
                        m.Loading++;
                        break;
                }
            }

            return m;
        }

        public static List<UrbanIndicator> Compute(UrbanMeasures m, UrbanRequirements r)
        {
            var rows = new List<UrbanIndicator>();
            bool hasParcel = m.Parcel > 1e-6;
            rows.Add(new UrbanIndicator("Površina parcele", Area(m.Parcel), string.Empty, null));
            rows.Add(new UrbanIndicator("Tlocrtna površina objekata", Area(m.Footprint), string.Empty, null));

            double kiz = hasParcel ? m.Footprint / m.Parcel : double.NaN;
            rows.Add(new UrbanIndicator("Koeficijent izgrađenosti (Kiz)", hasParcel ? Num(kiz, 2) : "nema parcele",
                r.MaxCoverage.HasValue ? "najviše " + Num(r.MaxCoverage.Value, 2) : string.Empty,
                r.MaxCoverage.HasValue && hasParcel ? kiz <= r.MaxCoverage.Value + 1e-9 : (bool?)null));

            double kis = hasParcel ? m.FloorArea / m.Parcel : double.NaN;
            rows.Add(new UrbanIndicator("Bruto građevinska površina (procjena iz etaža)", Area(m.FloorArea), string.Empty, null));
            rows.Add(new UrbanIndicator("Koeficijent iskorištenosti (Kis)", hasParcel ? Num(kis, 2) : "nema parcele",
                r.MaxFloorAreaRatio.HasValue ? "najviše " + Num(r.MaxFloorAreaRatio.Value, 2) : string.Empty,
                r.MaxFloorAreaRatio.HasValue && hasParcel ? kis <= r.MaxFloorAreaRatio.Value + 1e-9 : (bool?)null));

            double green = hasParcel ? 100 * m.Green / m.Parcel : double.NaN;
            rows.Add(new UrbanIndicator("Zelene površine", Area(m.Green) + (hasParcel ? " (" + Num(green, 1) + " %)" : string.Empty),
                r.MinGreenPercent.HasValue ? "najmanje " + Num(r.MinGreenPercent.Value, 1) + " %" : string.Empty,
                r.MinGreenPercent.HasValue && hasParcel ? green >= r.MinGreenPercent.Value - 1e-9 : (bool?)null));

            double perm = hasParcel ? 100 * m.Permeable / m.Parcel : double.NaN;
            rows.Add(new UrbanIndicator("Vodopropusne površine", Area(m.Permeable) + (hasParcel ? " (" + Num(perm, 1) + " %)" : string.Empty),
                r.MinPermeablePercent.HasValue ? "najmanje " + Num(r.MinPermeablePercent.Value, 1) + " %" : string.Empty,
                r.MinPermeablePercent.HasValue && hasParcel ? perm >= r.MinPermeablePercent.Value - 1e-9 : (bool?)null));

            int? needParking = RequiredParking(r);
            string parkingRule = needParking.HasValue
                ? "najmanje " + needParking.Value.ToString(CultureInfo.InvariantCulture) +
                  (r.Dwellings.HasValue && r.ParkingPerDwelling.HasValue ? " (" + r.Dwellings.Value + " stanova × " + Num(r.ParkingPerDwelling.Value, 2) + ")" : string.Empty)
                : string.Empty;
            rows.Add(new UrbanIndicator("Parking mjesta", m.Parking.ToString(CultureInfo.InvariantCulture), parkingRule,
                needParking.HasValue ? m.Parking >= needParking.Value : (bool?)null));

            int? needAccessible = r.MinAccessiblePercent.HasValue ? (int)Math.Ceiling(Math.Max(m.Parking, needParking ?? 0) * r.MinAccessiblePercent.Value / 100.0 - 1e-9) : (int?)null;
            if (needAccessible.HasValue && r.MinAccessiblePercent > 0)
            {
                needAccessible = Math.Max(1, needAccessible.Value);
            }

            rows.Add(new UrbanIndicator("Parking mjesta za osobe s invaliditetom", m.Accessible.ToString(CultureInfo.InvariantCulture),
                needAccessible.HasValue ? "najmanje " + needAccessible.Value + " (" + Num(r.MinAccessiblePercent!.Value, 1) + " %)" : string.Empty,
                needAccessible.HasValue ? m.Accessible >= needAccessible.Value : (bool?)null));

            rows.Add(new UrbanIndicator("Parking mjesta s punjačem za električna vozila", m.Ev.ToString(CultureInfo.InvariantCulture),
                r.MinEvSpaces.HasValue ? "najmanje " + r.MinEvSpaces.Value : string.Empty,
                r.MinEvSpaces.HasValue ? m.Ev >= r.MinEvSpaces.Value : (bool?)null));

            if (m.Loading > 0)
            {
                rows.Add(new UrbanIndicator("Mjesta za utovar / istovar", m.Loading.ToString(CultureInfo.InvariantCulture), string.Empty, null));
            }

            return rows;
        }

        public static int? RequiredParking(UrbanRequirements r)
        {
            int? fromDwellings = r.Dwellings.HasValue && r.ParkingPerDwelling.HasValue ? (int)Math.Ceiling(r.Dwellings.Value * r.ParkingPerDwelling.Value - 1e-9) : (int?)null;
            if (fromDwellings.HasValue && r.MinParking.HasValue)
            {
                return Math.Max(fromDwellings.Value, r.MinParking.Value);
            }

            return fromDwellings ?? r.MinParking;
        }

        /// <summary>The indicators as a schedule table for the drawing (Pokazatelj / Projektovano / Zahtjev plana / Ocjena).</summary>
        public static ScheduleTable ToTable(List<UrbanIndicator> rows)
        {
            var t = new ScheduleTable { Id = "urban", Title = "Urbanistički pokazatelji" };
            t.Columns.Add(new ScheduleColumn { Key = "name", Title = "Pokazatelj" });
            t.Columns.Add(new ScheduleColumn { Key = "value", Title = "Projektovano", Numeric = true });
            t.Columns.Add(new ScheduleColumn { Key = "req", Title = "Zahtjev plana" });
            t.Columns.Add(new ScheduleColumn { Key = "ok", Title = "Ocjena" });
            foreach (UrbanIndicator i in rows)
            {
                var row = new ScheduleRow();
                row.Cells.Add(i.Name);
                row.Cells.Add(i.Value);
                row.Cells.Add(i.Requirement.Length > 0 ? i.Requirement : "–");
                row.Cells.Add(i.Verdict);
                t.Rows.Add(row);
            }

            return t;
        }

        private static string Area(double m2) => Num(m2, 1) + " m²";

        private static string Num(double v, int decimals) => v.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), BsCulture);

        private static readonly CultureInfo BsCulture = CreateCulture();

        private static CultureInfo CreateCulture()
        {
            var c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            c.NumberFormat.NumberDecimalSeparator = ",";
            c.NumberFormat.NumberGroupSeparator = ".";
            return c;
        }
    }
}
