using System;
using System.Collections.Generic;
using SiteDesignPro.Core.Geometry;

namespace SiteDesignPro.Core.Model
{
    /// <summary>
    /// A managed site design element: stable identity, catalog type, geometry (metres, package frame) and data.
    /// Category, builder, layer and model group are resolved from the catalog type.
    /// </summary>
    public sealed class SiteElement
    {
        public SiteElement(string id, string typeId, ElementGeometry geometry)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }

        /// <summary>Stable unique identifier (lower-case UUID). Never reused, survives save/reopen and export.</summary>
        public string Id { get; set; }

        public string TypeId { get; set; }

        /// <summary>Display name; defaults to the type name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Human-readable label / object number, e.g. "T-012".</summary>
        public string? Label { get; set; }

        public ElementGeometry Geometry { get; set; }

        /// <summary>Catalog parameters (metres for lengths, degrees for angle parameters).</summary>
        public ParameterSet Parameters { get; set; } = new ParameterSet();

        /// <summary>Descriptive properties (manufacturer, botanical name, notes ...).</summary>
        public SortedDictionary<string, string> Properties { get; set; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>User-defined fields that are not part of the catalog schema.</summary>
        public ParameterSet Custom { get; set; } = new ParameterSet();

        /// <summary>Incremented whenever the element data or geometry changes in the source application.</summary>
        public int Revision { get; set; } = 1;

        /// <summary>Host object reference (e.g. AutoCAD handle) for diagnostics; not used for identity.</summary>
        public string? SourceHandle { get; set; }

        /// <summary>Optional reference to a package asset (custom component, texture).</summary>
        public string? AssetId { get; set; }

        public static string NewId() => Guid.NewGuid().ToString("D");

        public static bool IsValidId(string? id) => id != null && id.Length == 36 && Guid.TryParseExact(id, "D", out _) && id == id.ToLowerInvariant();

        public string Property(string key) => Properties.TryGetValue(key, out string? v) ? v : string.Empty;
    }
}
