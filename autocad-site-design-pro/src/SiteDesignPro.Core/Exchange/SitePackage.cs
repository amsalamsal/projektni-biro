using System;
using System.Collections.Generic;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Exchange
{
    /// <summary>Current exchange schema version written by this library.</summary>
    public static class PackageFormat
    {
        public const string FormatId = "site-design-pro-package";
        public const string SchemaVersion = "1.0.0";
        public const int SupportedMajor = 1;
        public const string ManifestFile = "manifest.json";
        public const string GeometryFile = "geometry.json";
        public const string AssetsFolder = "assets";
        public const string PreviewFolder = "preview";
        public const string ValidationReportFile = "preview/validation-report.json";
        public const string PackageExtension = ".sdpkg";
        public const string ApplicationName = "AutoCAD Site Design Pro";
    }

    /// <summary>Element as read from a package: the element plus metadata stored with it.</summary>
    public sealed class PackageElement
    {
        public PackageElement(SiteElement element)
        {
            Element = element;
        }

        public SiteElement Element { get; }

        public string Category { get; set; } = string.Empty;

        public string Subcategory { get; set; } = string.Empty;

        public string Builder { get; set; } = string.Empty;

        public string Layer { get; set; } = string.Empty;

        public string ModelGroup { get; set; } = string.Empty;

        public string ContentHash { get; set; } = string.Empty;
    }

    public sealed class PackageAsset
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>Package-relative path beginning with "assets/".</summary>
        public string Path { get; set; } = string.Empty;

        public string Kind { get; set; } = "texture";

        public string? Sha256 { get; set; }

        public string? Description { get; set; }

        /// <summary>Local file to copy into the package when writing (not serialised).</summary>
        public string? SourceFile { get; set; }
    }

    /// <summary>In-memory representation of a package read from disk.</summary>
    public sealed class SitePackage
    {
        public string SchemaVersion { get; set; } = PackageFormat.SchemaVersion;

        public string PackageId { get; set; } = string.Empty;

        public DateTime CreatedUtc { get; set; }

        public string GeneratorApplication { get; set; } = string.Empty;

        public string GeneratorVersion { get; set; } = string.Empty;

        public string ProjectId { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;

        public string SourceLengthUnit { get; set; } = "m";

        public double SourceUnitsToMeters { get; set; } = 1;

        public double[] Origin { get; set; } = new double[3];

        public double TrueNorthDegrees { get; set; }

        public string CatalogId { get; set; } = string.Empty;

        public string CatalogVersion { get; set; } = string.Empty;

        public List<PackageElement> Elements { get; } = new List<PackageElement>();

        public List<PackageAsset> Assets { get; } = new List<PackageAsset>();

        /// <summary>Raw type definition ids included in the manifest.</summary>
        public List<string> TypeDefinitionIds { get; } = new List<string>();
    }
}
