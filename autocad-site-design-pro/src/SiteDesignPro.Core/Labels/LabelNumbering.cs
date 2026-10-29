using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Labels
{
    /// <summary>Generates element labels such as "T-001" or "T-A-012" according to the project label settings.</summary>
    public sealed class LabelNumbering
    {
        private readonly LabelSettings settings;
        private readonly ElementCatalog catalog;

        public LabelNumbering(LabelSettings settings, ElementCatalog catalog)
        {
            this.settings = settings;
            this.catalog = catalog;
        }

        /// <summary>Sequence key for a type: its label prefix, or its category when numbering by category.</summary>
        public string SequenceKey(ElementType type) => settings.NumberingScope == "category" ? type.Category : type.LabelPrefix;

        public string Format(string prefix, int number)
        {
            string zone = string.IsNullOrWhiteSpace(settings.Zone) ? string.Empty : settings.Zone + settings.Separator;
            return prefix + settings.Separator + zone + number.ToString(new string('0', Math.Max(1, settings.NumberWidth)), CultureInfo.InvariantCulture);
        }

        /// <summary>Extracts the trailing number of a label that matches prefix/zone, or null.</summary>
        public int? ParseNumber(string? label, string prefix)
        {
            if (string.IsNullOrEmpty(label))
            {
                return null;
            }

            string zone = string.IsNullOrWhiteSpace(settings.Zone) ? string.Empty : settings.Zone + settings.Separator;
            string head = prefix + settings.Separator + zone;
            if (!label.StartsWith(head, StringComparison.Ordinal))
            {
                return null;
            }

            return int.TryParse(label.Substring(head.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : (int?)null;
        }

        /// <summary>Next free label for a type, given every existing element.</summary>
        public string Next(ElementType type, IEnumerable<SiteElement> existing)
        {
            string prefix = type.LabelPrefix;
            int max = 0;
            foreach (SiteElement e in existing)
            {
                int? n = ParseNumber(e.Label, prefix);
                if (n.HasValue && n.Value > max)
                {
                    max = n.Value;
                }
            }

            return Format(prefix, max + 1);
        }

        /// <summary>
        /// Assigns labels to elements without a label (or to all when <paramref name="renumberAll"/>), numbering each
        /// prefix in reading order: rows from north to south (row height <paramref name="rowTolerance"/>), west to east.
        /// Returns the elements whose label changed.
        /// </summary>
        public List<SiteElement> Assign(IList<SiteElement> elements, bool renumberAll, double rowTolerance)
        {
            var changed = new List<SiteElement>();
            foreach (IGrouping<string, SiteElement> group in elements.Where(e => catalog.FindType(e.TypeId) != null).GroupBy(e => catalog.FindType(e.TypeId)!.LabelPrefix, StringComparer.Ordinal))
            {
                string prefix = group.Key;
                List<SiteElement> ordered = group.OrderByDescending(e => Math.Floor(Anchor(e).Y / Math.Max(1e-9, rowTolerance))).ThenBy(e => Anchor(e).X).ToList();
                int next = 1;
                if (!renumberAll)
                {
                    next = ordered.Select(e => ParseNumber(e.Label, prefix) ?? 0).DefaultIfEmpty(0).Max() + 1;
                }

                foreach (SiteElement e in ordered)
                {
                    if (!renumberAll && !string.IsNullOrEmpty(e.Label))
                    {
                        continue;
                    }

                    string label = Format(prefix, next++);
                    if (e.Label != label)
                    {
                        e.Label = label;
                        changed.Add(e);
                    }
                }
            }

            return changed;
        }

        /// <summary>Representative plan point of an element (insertion, path midpoint or boundary interior point).</summary>
        public static Vec2 Anchor(SiteElement e)
        {
            switch (e.Geometry)
            {
                case PointGeometry p:
                    return p.Position.XY;
                case PathGeometry path:
                    return GeometryMath.PointAtDistance(path.Ring, path.Closed, GeometryMath.Length(path.Ring, path.Closed) / 2).Point.XY;
                case PolygonGeometry poly:
                    {
                        IReadOnlyList<Vec2> pts = GeometryMath.PlanOutline(poly);
                        return GeometryMath.InteriorPoint(pts) ?? pts[0];
                    }

                default:
                    return Vec2.Zero;
            }
        }
    }
}
