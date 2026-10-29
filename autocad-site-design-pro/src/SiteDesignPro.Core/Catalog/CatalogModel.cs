using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Catalog
{
    public enum ParameterType
    {
        Length,
        Number,
        Integer,
        Angle,
        Bool,
        Enum,
        Text,
        Material,
    }

    public sealed class ParameterDefinition
    {
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public ParameterType Type { get; set; }

        public string? Unit { get; set; }

        public ParamValue Default { get; set; }

        public double? Min { get; set; }

        public double? Max { get; set; }

        public IReadOnlyList<string> Options { get; set; } = Array.Empty<string>();

        public bool Required { get; set; }

        public string Group { get; set; } = "General";

        public string? Description { get; set; }

        /// <summary>Unit label for display: m for lengths, deg for angles, or the declared unit.</summary>
        public string DisplayUnit => Type switch
        {
            ParameterType.Length => "m",
            ParameterType.Angle => "deg",
            _ => Unit ?? string.Empty,
        };

        public bool IsNumeric => Type == ParameterType.Length || Type == ParameterType.Number || Type == ParameterType.Integer || Type == ParameterType.Angle;

        public ParameterDefinition WithDefault(ParamValue value)
        {
            ParameterDefinition copy = (ParameterDefinition)MemberwiseClone();
            copy.Default = value;
            return copy;
        }

        public static string TypeCode(ParameterType t) => t.ToString().ToLowerInvariant();
    }

    public sealed class HatchDefinition
    {
        public string Pattern { get; set; } = "SOLID";

        /// <summary>Pattern scale for a metre-based drawing; scaled by drawing units per metre / 1000 for acadiso patterns.</summary>
        public double Scale { get; set; } = 1.0;

        public double Angle { get; set; }
    }

    public sealed class QuantityDefinition
    {
        /// <summary>count, length, area.</summary>
        public string Measure { get; set; } = "count";

        public string Unit { get; set; } = "pcs";

        /// <summary>Optional secondary measure: volume, area, perimeter.</summary>
        public string? Secondary { get; set; }

        /// <summary>Integer parameter that multiplies a count (e.g. number of stands in a rack).</summary>
        public string? CountParam { get; set; }

        /// <summary>Product of parameters, e.g. "rows*modulesPerRow".</summary>
        public string? CountExpression { get; set; }
    }

    /// <summary>Parametric generator implemented by both host applications.</summary>
    public sealed class BuilderDefinition
    {
        public string Id { get; set; } = string.Empty;

        public GeometryKind Geometry { get; set; }

        public string Description { get; set; } = string.Empty;

        public QuantityDefinition Quantity { get; set; } = new QuantityDefinition();

        public string? Symbol { get; set; }

        public HatchDefinition? Hatch { get; set; }

        public string? PolylineWidthParam { get; set; }

        public string? Linetype { get; set; }

        public IReadOnlyList<ParameterDefinition> Parameters { get; set; } = Array.Empty<ParameterDefinition>();
    }

    public sealed class CategoryDefinition
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Panel { get; set; } = string.Empty;

        public string ModelGroup { get; set; } = string.Empty;
    }

    public sealed class ModelGroupDefinition
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Tag { get; set; } = string.Empty;

        /// <summary>Tag name used before the Bosnian catalog (models imported with version 0.1).</summary>
        public string LegacyTag { get; set; } = string.Empty;

        public int Order { get; set; }
    }

    public sealed class ScheduleDefinition
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();

        public IReadOnlyList<string> GroupBy { get; set; } = Array.Empty<string>();
    }

    public sealed class PropertyDefinition
    {
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string Group { get; set; } = "General";
    }

    public sealed class LayerDefinition
    {
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>AutoCAD Colour Index 1-255.</summary>
        public short Color { get; set; } = 7;

        public string Linetype { get; set; } = "Continuous";

        /// <summary>Lineweight in hundredths of a millimetre; -3 = default.</summary>
        public int Lineweight { get; set; } = -3;

        /// <summary>Transparency percentage 0-90.</summary>
        public int Transparency { get; set; }

        public bool Plot { get; set; } = true;

        public string Description { get; set; } = string.Empty;

        /// <summary>Layer name used before the Bosnian layer standard; renamed by SDPLAYERS when found.</summary>
        public string LegacyName { get; set; } = string.Empty;
    }

    public sealed class MaterialDefinition
    {
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int[] Color { get; set; } = new[] { 200, 200, 200 };

        public double Alpha { get; set; } = 1.0;

        public string? Texture { get; set; }

        public double[]? TextureSize { get; set; }
    }

    /// <summary>Resolved element type: catalog entry merged with its builder definition.</summary>
    public sealed class ElementType
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        /// <summary>English name (search and reference); the display name is Bosnian.</summary>
        public string NameEn { get; set; } = string.Empty;

        public string Subcategory { get; set; } = string.Empty;

        public BuilderDefinition Builder { get; set; } = new BuilderDefinition();

        public string LayerKey { get; set; } = string.Empty;

        public string ModelGroup { get; set; } = string.Empty;

        public string LabelPrefix { get; set; } = "E";

        public string ScheduleId { get; set; } = "general";

        public HatchDefinition? Hatch { get; set; }

        /// <summary>Parameters with type-level defaults applied.</summary>
        public IReadOnlyList<ParameterDefinition> Parameters { get; set; } = Array.Empty<ParameterDefinition>();

        /// <summary>Type-specific property keys (in addition to common properties).</summary>
        public IReadOnlyList<string> PropertyKeys { get; set; } = Array.Empty<string>();

        /// <summary>Parameters ("key") or properties ("prop:key") that require professional input.</summary>
        public IReadOnlyList<string> ReviewRequired { get; set; } = Array.Empty<string>();

        /// <summary>Additional Ribbon panels that list this type.</summary>
        public IReadOnlyList<string> ExtraPanels { get; set; } = Array.Empty<string>();

        /// <summary>Origin of the definition: "core" or a user catalog file name.</summary>
        public string Source { get; set; } = "core";

        public GeometryKind Geometry => Builder.Geometry;

        public ParameterDefinition? Parameter(string key) => Parameters.FirstOrDefault(p => p.Key == key);

        /// <summary>New parameter set filled with defaults.</summary>
        public ParameterSet CreateDefaultParameters()
        {
            var set = new ParameterSet();
            foreach (ParameterDefinition p in Parameters)
            {
                set[p.Key] = p.Default;
            }

            return set;
        }
    }
}
