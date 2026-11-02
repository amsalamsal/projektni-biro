namespace SiteDesignPro.AutoCAD.Commands
{
    internal enum StreetPartKind
    {
        /// <summary>Path element on the axis itself (the carriageway or the path).</summary>
        Axis,

        /// <summary>Path element parallel to the axis at an offset.</summary>
        Line,

        /// <summary>Area between two parallel offsets (sidewalk, green strip).</summary>
        Band,

        /// <summary>Point elements repeated along a parallel line (trees, lights, benches).</summary>
        Row,
    }

    internal sealed class StreetPart
    {
        public StreetPart(StreetPartKind kind, string type, double offset, double size = 0, bool both = true, double spacing = 0)
        {
            Kind = kind;
            Type = type;
            Offset = offset;
            Size = size;
            Both = both;
            Spacing = spacing;
        }

        public StreetPartKind Kind { get; }

        public string Type { get; }

        /// <summary>Metres from the axis, positive to the left of the drawing direction (Band: inner edge).</summary>
        public double Offset { get; }

        /// <summary>Width of a Line / Axis element or of a Band (metres).</summary>
        public double Size { get; }

        /// <summary>Mirrored to the right side as well.</summary>
        public bool Both { get; }

        public double Spacing { get; }
    }

    internal sealed class StreetProfile
    {
        public StreetProfile(string key, string name, string description, double axisWidth, params StreetPart[] parts)
        {
            Key = key;
            Name = name;
            Description = description;
            AxisWidth = axisWidth;
            Parts = parts;
        }

        public string Key { get; }

        public string Name { get; }

        public string Description { get; }

        public double AxisWidth { get; }

        public StreetPart[] Parts { get; }
    }
}
