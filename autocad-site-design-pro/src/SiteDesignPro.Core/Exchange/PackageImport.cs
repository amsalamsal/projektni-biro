using System;
using System.Collections.Generic;
using SiteDesignPro.Core.Elevation;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Units;

namespace SiteDesignPro.Core.Exchange
{
    /// <summary>
    /// Brings a package back into a drawing: the inverse of <see cref="PackageWriter.ToPackageFrame"/>.
    /// Used by SDPUVOZPAKETA (a package made elsewhere, e.g. converted from another DWG, becomes editable elements).
    /// </summary>
    public static class PackageImport
    {
        /// <summary>
        /// Takes over the package's unit, origin, project identity and vertical datum (for a drawing without elements).
        /// Values the package does not state stay as they are; nothing is defaulted.
        /// </summary>
        public static void AdoptProjectFrame(SitePackage package, ProjectSettings settings)
        {
            LengthUnit unit;
            try
            {
                unit = UnitConversion.Parse(package.SourceLengthUnit);
            }
            catch (ArgumentException)
            {
                unit = LengthUnit.Meter;
            }

            settings.DrawingUnit = UnitConversion.Code(unit);
            double k = settings.Scale.DrawingUnitsPerMeter;
            double[] o = package.Origin ?? new double[3];
            settings.Origin = new[] { At(o, 0) * k, At(o, 1) * k, At(o, 2) * k };
            settings.TrueNorthDegrees = package.TrueNorthDegrees;
            if (!string.IsNullOrEmpty(package.ProjectId))
            {
                settings.ProjectId = package.ProjectId;
            }

            if (!string.IsNullOrEmpty(package.ProjectName))
            {
                settings.Name = package.ProjectName;
            }

            if (!string.IsNullOrEmpty(package.Crs))
            {
                settings.Crs = package.Crs;
            }

            if (package.BuildingZeroAbsolute.HasValue)
            {
                ElevationDatum d = settings.Elevations;
                d.BuildingZeroAbsolute = package.BuildingZeroAbsolute;
                if (!string.IsNullOrEmpty(package.VerticalDatum))
                {
                    d.VerticalDatum = package.VerticalDatum;
                }

                // the exporter states the mode; older packages without it: drawing Z is relative when package z = 0
                // sits at ±0,00 (the writer's relative mode)
                double originZ = At(o, 2);
                bool relative = package.DrawingZ == "relative" || (package.DrawingZ != "absolute" &&
                    package.PackageZeroAbsolute.HasValue && Math.Abs(package.PackageZeroAbsolute.Value - (package.BuildingZeroAbsolute.Value + originZ)) < 1e-6);
                d.DrawingZ = relative ? DrawingZMode.Relative : DrawingZMode.Absolute;
            }
        }

        /// <summary>Package frame (metres relative to the origin) → drawing frame (drawing units, WCS).</summary>
        public static SiteElement ToDrawingFrame(SiteElement packageElement, ProjectSettings settings)
        {
            UnitScale scale = settings.Scale;
            Vec3 origin = settings.OriginMeters;
            Vec3 Map(Vec3 v) => new Vec3(scale.ToDrawing(v.X + origin.X), scale.ToDrawing(v.Y + origin.Y), scale.ToDrawing(v.Z + origin.Z));
            return new SiteElement(packageElement.Id, packageElement.TypeId, packageElement.Geometry.Transform(Map))
            {
                Name = packageElement.Name,
                Label = packageElement.Label,
                Parameters = packageElement.Parameters.Clone(),
                Properties = new SortedDictionary<string, string>(packageElement.Properties, StringComparer.Ordinal),
                Custom = packageElement.Custom.Clone(),
                Revision = packageElement.Revision,
                SourceHandle = packageElement.SourceHandle,
                AssetId = packageElement.AssetId,
            };
        }

        private static double At(double[] a, int i) => a.Length > i ? a[i] : 0.0;
    }
}
