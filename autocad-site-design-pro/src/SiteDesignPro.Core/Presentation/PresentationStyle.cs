using System;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Presentation
{
    /// <summary>How an element is painted on the colour presentation plan.</summary>
    public enum PresentationKind
    {
        /// <summary>Not painted (boundaries, zones, small equipment).</summary>
        None,

        /// <summary>Area filled with a solid colour.</summary>
        Fill,

        /// <summary>Path painted as a band of its width (roads, paths).</summary>
        Band,

        /// <summary>Tree or shrub: canopy disc with a shadow.</summary>
        Canopy,

        /// <summary>Building: light fill with a cast shadow.</summary>
        Building,
    }

    /// <summary>Colour (RGB), transparency (percent) and kind of one element on the presentation plan.</summary>
    public readonly struct PresentationPaint
    {
        public PresentationPaint(PresentationKind kind, int r, int g, int b, int transparency, int layer)
        {
            Kind = kind;
            R = r;
            G = g;
            B = b;
            Transparency = transparency;
            Layer = layer;
        }

        public PresentationKind Kind { get; }

        public int R { get; }

        public int G { get; }

        public int B { get; }

        /// <summary>0 = opaque, 100 = invisible.</summary>
        public int Transparency { get; }

        /// <summary>Stacking order from the ground up (0 ground surfaces, 1 water, 2 paths, 3 shadows, 4 buildings, 5 canopies).</summary>
        public int Layer { get; }

        public static PresentationPaint None => new PresentationPaint(PresentationKind.None, 0, 0, 0, 0, 0);
    }

    /// <summary>
    /// Colours for the presentation plan: surfaces in the colour of their material, planting in greens, water blue,
    /// trees as canopies with shadows, buildings light with a cast shadow. Same palette as the SketchUp materials.
    /// </summary>
    public static class PresentationStyle
    {
        public static readonly (int R, int G, int B) Shadow = (45, 45, 50);

        public static PresentationPaint For(ElementType t, SiteElement e, ElementCatalog catalog)
        {
            string builder = t.Builder.Id;
            switch (builder)
            {
                case "tree":
                    {
                        if (t.Id == "landscape.tree_evergreen")
                        {
                            return new PresentationPaint(PresentationKind.Canopy, 62, 120, 72, 15, 5);
                        }

                        if (t.Id == "landscape.tree_existing")
                        {
                            return new PresentationPaint(PresentationKind.Canopy, 128, 150, 98, 20, 5);
                        }

                        if (t.Id.Contains("fruit") || t.Id.Contains("orchard"))
                        {
                            return new PresentationPaint(PresentationKind.Canopy, 132, 178, 88, 15, 5);
                        }

                        return new PresentationPaint(PresentationKind.Canopy, 92, 152, 70, 15, 5);
                    }

                case "shrub":
                    return new PresentationPaint(PresentationKind.Canopy, 112, 162, 82, 10, 5);
                case "massing":
                    return new PresentationPaint(PresentationKind.Building, 238, 234, 226, 0, 4);
                case "pool":
                    return new PresentationPaint(PresentationKind.Fill, 104, 178, 228, 0, 1);
                case "basin":
                    return t.Id == "water.retention_basin"
                        ? new PresentationPaint(PresentationKind.Fill, 120, 176, 205, 0, 1)
                        : new PresentationPaint(PresentationKind.Fill, 126, 172, 132, 0, 1);
                case "planting_area":
                    {
                        switch (t.Id)
                        {
                            case "landscape.flower_bed":
                            case "landscape.rose_garden":
                                return new PresentationPaint(PresentationKind.Fill, 222, 168, 186, 0, 0);
                            case "landscape.ornamental_grasses":
                                return new PresentationPaint(PresentationKind.Fill, 196, 196, 128, 0, 0);
                            case "landscape.perennials":
                                return new PresentationPaint(PresentationKind.Fill, 176, 196, 118, 0, 0);
                            case "landscape.meadow":
                                return new PresentationPaint(PresentationKind.Fill, 172, 204, 112, 0, 0);
                            case "landscape.lawn_area":
                                return new PresentationPaint(PresentationKind.Fill, 152, 202, 104, 0, 0);
                            default:
                                return new PresentationPaint(PresentationKind.Fill, 118, 166, 86, 0, 0);
                        }
                    }
            }

            if (t.Id == "surface.lawn")
            {
                return new PresentationPaint(PresentationKind.Fill, 152, 202, 104, 0, 0);
            }

            if (t.Category == "site" || t.Builder.Geometry == Geometry.GeometryKind.Point)
            {
                return PresentationPaint.None;
            }

            // zones, markings and placeholders stay as linework
            if (builder == "zone" || builder == "stripes")
            {
                return PresentationPaint.None;
            }

            (int r, int g, int b)? c = MaterialColor(t, e, catalog);
            if (t.Builder.Geometry == Geometry.GeometryKind.Path)
            {
                if (t.Builder.PolylineWidthParam != "width" || !c.HasValue)
                {
                    return PresentationPaint.None;
                }

                return new PresentationPaint(PresentationKind.Band, c.Value.r, c.Value.g, c.Value.b, 0, 2);
            }

            if (t.Builder.Geometry == Geometry.GeometryKind.Polygon && c.HasValue)
            {
                return new PresentationPaint(PresentationKind.Fill, c.Value.r, c.Value.g, c.Value.b, 0, t.Category == "roads" ? 2 : 0);
            }

            return PresentationPaint.None;
        }

        /// <summary>Colour of the element's material parameter (own value or the type default), or null.</summary>
        public static (int R, int G, int B)? MaterialColor(ElementType t, SiteElement e, ElementCatalog catalog)
        {
            string? key = null;
            if (e.Parameters.Contains("material") && !e.Parameters["material"].IsNull)
            {
                key = e.Parameters["material"].AsText();
            }
            else if (t.Parameter("material") is ParameterDefinition def && !def.Default.IsNull)
            {
                key = def.Default.AsText();
            }

            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            MaterialDefinition? m = catalog.FindMaterial(key!);
            if (m == null || m.Color == null || m.Color.Length < 3)
            {
                return null;
            }

            return (Clamp(m.Color[0]), Clamp(m.Color[1]), Clamp(m.Color[2]));
        }

        /// <summary>Direction of cast shadows in drawing coordinates (towards north-east of true north).</summary>
        public static (double X, double Y) ShadowDirection(double trueNorthDegrees)
        {
            double a = (trueNorthDegrees + 45) * Math.PI / 180;
            return (Math.Sin(a), Math.Cos(a));
        }

        private static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;
    }
}
