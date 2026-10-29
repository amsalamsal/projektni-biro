using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Units;

namespace SiteDesignPro.Core.Model
{
    /// <summary>Annotation and numbering preferences.</summary>
    public sealed class LabelSettings
    {
        /// <summary>Plotted text height in millimetres on paper.</summary>
        public double TextHeightMm { get; set; } = 2.5;

        /// <summary>Drawing scale denominator used for model-space annotation (1:N).</summary>
        public double ScaleDenominator { get; set; } = 200;

        public string Separator { get; set; } = "-";

        public int NumberWidth { get; set; } = 3;

        /// <summary>"prefix" = one sequence per label prefix; "category" = one sequence per category.</summary>
        public string NumberingScope { get; set; } = "prefix";

        /// <summary>Optional zone code inserted before the number, e.g. "A" gives "T-A-001".</summary>
        public string Zone { get; set; } = string.Empty;

        public int LinearPrecision { get; set; } = 2;

        public int AreaPrecision { get; set; } = 1;

        /// <summary>Model-space text height in metres for the current scale.</summary>
        public double ModelTextHeightMeters => TextHeightMm / 1000.0 * ScaleDenominator;
    }

    /// <summary>Thresholds used by validation. Values are project rules supplied by the user, not regulations.</summary>
    public sealed class CheckSettings
    {
        /// <summary>Minimum plan clearance between point elements before a potential collision is reported (metres).</summary>
        public double CollisionTolerance { get; set; } = 0.0;

        /// <summary>Report overlapping surface boundaries.</summary>
        public bool CheckSurfaceOverlaps { get; set; } = true;

        /// <summary>Report mature canopy conflicts between trees and buildings.</summary>
        public bool CheckCanopyClearance { get; set; } = true;

        /// <summary>Elevations outside +/- this range (metres, relative to project origin) are flagged.</summary>
        public double ElevationRange { get; set; } = 500;
    }

    /// <summary>Project-level settings stored with the drawing and written to the package manifest.</summary>
    public sealed class ProjectSettings
    {
        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        public int SettingsVersion { get; set; } = 1;

        public string ProjectId { get; set; } = Guid.NewGuid().ToString("D");

        public string Name { get; set; } = "Untitled site project";

        public string Number { get; set; } = string.Empty;

        public string Client { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;

        public string Revision { get; set; } = "A";

        /// <summary>Drawing length unit code (mm, cm, m, in, ft).</summary>
        public string DrawingUnit { get; set; } = "mm";

        /// <summary>Project origin in drawing units (WCS). Maps to package (0,0,0).</summary>
        public double[] Origin { get; set; } = new double[] { 0, 0, 0 };

        /// <summary>Clockwise angle from drawing +Y to true north, degrees.</summary>
        public double TrueNorthDegrees { get; set; }

        public string Crs { get; set; } = string.Empty;

        public string HeightDatum { get; set; } = string.Empty;

        public LabelSettings Labels { get; set; } = new LabelSettings();

        public CheckSettings Checks { get; set; } = new CheckSettings();

        public string LastExportPath { get; set; } = string.Empty;

        [JsonIgnore]
        public LengthUnit Unit => UnitConversion.Parse(DrawingUnit);

        [JsonIgnore]
        public UnitScale Scale => new UnitScale(Unit);

        /// <summary>Origin converted to metres.</summary>
        [JsonIgnore]
        public Vec3 OriginMeters
        {
            get
            {
                UnitScale s = Scale;
                return new Vec3(s.ToMeters(Origin[0]), s.ToMeters(Origin[1]), s.ToMeters(Origin.Length > 2 ? Origin[2] : 0));
            }
        }

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        public static ProjectSettings FromJson(string json)
        {
            ProjectSettings? s = JsonSerializer.Deserialize<ProjectSettings>(json, JsonOptions);
            if (s == null)
            {
                throw new FormatException("Project settings JSON is empty.");
            }

            if (s.Origin == null || s.Origin.Length < 2)
            {
                s.Origin = new double[] { 0, 0, 0 };
            }

            s.Labels ??= new LabelSettings();
            s.Checks ??= new CheckSettings();
            UnitConversion.Parse(s.DrawingUnit); // validates
            return s;
        }
    }
}
