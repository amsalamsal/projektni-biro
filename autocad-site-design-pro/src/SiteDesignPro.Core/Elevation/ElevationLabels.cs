using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.Core.Elevation
{
    public enum LabelKind
    {
        Relative,
        Absolute,

        /// <summary>The text alone does not say whether the value is relative or absolute.</summary>
        Ambiguous,
        Invalid,
    }

    /// <summary>Result of reading one elevation label text.</summary>
    public sealed class ParsedElevation
    {
        public LabelKind Kind { get; set; }

        public double? Relative { get; set; }

        public double? Absolute { get; set; }

        /// <summary>Value for ambiguous labels.</summary>
        public double? Value { get; set; }

        public string CleanText { get; set; } = string.Empty;

        public string? Problem { get; set; }
    }

    /// <summary>
    /// Reads elevation labels as they appear in Bosnian drawings: "±0.00", "%%p0,00", "+0,25", "-0.15", "KT 518,81",
    /// "518.81 m n.v.", "▼+0,15", "NK=518,56 / ±0,00". It never guesses: a value that could be relative or absolute
    /// is returned as <see cref="LabelKind.Ambiguous"/> and the user decides.
    /// </summary>
    public static class ElevationLabelParser
    {
        private static readonly Regex Number = new Regex(@"(?<sign>[+\-±]?)\s*(?<num>\d{1,5}(?:[.,]\d{1,4})?)", RegexOptions.CultureInvariant);
        private static readonly Regex MTextCodes = new Regex(@"\\[ACFHQTWfhqtw][^;]*;|\\[LlOoKkPpNX~]|[{}]", RegexOptions.CultureInvariant);
        private static readonly Regex AbsoluteMarker = new Regex(@"(m\.?\s*n\.?\s*v\.?|\bn\.?\s*v\.?\b|\bapsol|\bnmv\b|\bmnv\b|\ba\.?s\.?l\.?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Values with |v| at or above this (metres) are treated as absolute candidates.</summary>
        public const double DefaultAbsoluteThreshold = 50.0;

        public static string Clean(string raw)
        {
            string s = raw ?? string.Empty;
            s = s.Replace("%%p", "±").Replace("%%P", "±").Replace("%%d", "°").Replace("%%D", "°").Replace("%%c", "⌀").Replace("%%C", "⌀");
            s = s.Replace("\\U+00B1", "±").Replace("\\u+00b1", "±").Replace("−", "-");
            s = MTextCodes.Replace(s, " ");
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        public static ParsedElevation Parse(string raw, double absoluteThreshold = DefaultAbsoluteThreshold)
        {
            string text = Clean(raw);
            var result = new ParsedElevation { CleanText = text };
            bool absoluteMarker = AbsoluteMarker.IsMatch(text);
            var values = new List<(char Sign, double Value)>();
            foreach (Match m in Number.Matches(text))
            {
                string sign = m.Groups["sign"].Value;
                if (!Bs.TryParse(m.Groups["num"].Value, out double v))
                {
                    continue;
                }

                char sg = sign.Length == 0 ? ' ' : sign[0];
                values.Add((sg, sg == '-' ? -v : v));
            }

            if (values.Count == 0)
            {
                result.Kind = LabelKind.Invalid;
                result.Problem = Bs.T("U tekstu nije pronađena vrijednost kote.");
                return result;
            }

            if (values.Count > 2)
            {
                result.Kind = LabelKind.Invalid;
                result.Problem = Bs.T("Tekst sadrži više od dvije brojčane vrijednosti; kota nije jednoznačna.");
                return result;
            }

            foreach ((char sign, double v) in values)
            {
                LabelKind k = Classify(sign, v, absoluteMarker && values.Count == 1, absoluteThreshold);
                switch (k)
                {
                    case LabelKind.Relative when result.Relative == null:
                        result.Relative = v;
                        break;
                    case LabelKind.Absolute when result.Absolute == null:
                        result.Absolute = v;
                        break;
                    case LabelKind.Ambiguous when result.Value == null:
                        result.Value = v;
                        break;
                    default:
                        result.Kind = LabelKind.Invalid;
                        result.Problem = Bs.T("Tekst sadrži dvije kote iste vrste; nije jasno koja vrijedi.");
                        return result;
                }
            }

            if (result.Value != null && (result.Relative != null || result.Absolute != null))
            {
                // the other value disambiguates nothing: report
                result.Kind = LabelKind.Ambiguous;
                result.Problem = Bs.T("Jedna od dvije vrijednosti nije jednoznačna (relativna ili apsolutna).");
                return result;
            }

            if (result.Relative != null && result.Absolute != null)
            {
                result.Kind = LabelKind.Absolute; // pair: both known, checked against the datum by the validator
            }
            else if (result.Relative != null)
            {
                result.Kind = LabelKind.Relative;
            }
            else if (result.Absolute != null)
            {
                result.Kind = LabelKind.Absolute;
            }
            else
            {
                result.Kind = LabelKind.Ambiguous;
                result.Problem = Bs.T("Vrijednost {0} može biti relativna ili apsolutna kota; odredite vrstu ručno.", Bs.Num(result.Value!.Value, 2));
            }

            return result;
        }

        private static LabelKind Classify(char sign, double v, bool absoluteMarker, double threshold)
        {
            double a = Math.Abs(v);
            if (absoluteMarker)
            {
                return LabelKind.Absolute;
            }

            if (sign == '±')
            {
                return LabelKind.Relative;
            }

            if (sign == '+' || sign == '-')
            {
                return a < threshold ? LabelKind.Relative : LabelKind.Ambiguous;
            }

            return a >= threshold ? LabelKind.Absolute : LabelKind.Ambiguous;
        }
    }

    /// <summary>An elevation label found in the source drawing.</summary>
    public sealed class SourceLabel
    {
        public string Handle { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public Vec3 Position { get; set; }

        /// <summary>Leader end point when the label is a multileader/leader (points at the object).</summary>
        public Vec3? Target { get; set; }
    }

    /// <summary>A closed or open target an elevation may be assigned to.</summary>
    public sealed class AssignmentTarget
    {
        public string Handle { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public Ring Boundary { get; set; } = new Ring(new List<Vec3>());

        public bool Closed { get; set; }

        public double Area { get; set; }
    }

    public enum ProposalStatus
    {
        /// <summary>Exactly one target found; ready to apply after confirmation.</summary>
        Proposed,

        /// <summary>No target contains the label point.</summary>
        NoTarget,

        /// <summary>Several equally small targets contain the point.</summary>
        AmbiguousTarget,

        /// <summary>The label text cannot be used.</summary>
        BadLabel,

        /// <summary>Absolute and relative values in the label do not agree with the datum.</summary>
        Conflict,
    }

    public sealed class AssignmentProposal
    {
        public SourceLabel Label { get; set; } = new SourceLabel();

        public ParsedElevation Parsed { get; set; } = new ParsedElevation();

        public AssignmentTarget? Target { get; set; }

        public ProposalStatus Status { get; set; }

        public double? RelativeElevation { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Proposes which single object each elevation label belongs to. A label applies only to the smallest closed
    /// boundary containing its leader target (or insertion point); nothing is assigned when no object or more than
    /// one equally small object qualifies. Proposals are shown to the user before anything is changed.
    /// </summary>
    public static class ElevationAssigner
    {
        public static List<AssignmentProposal> Propose(IEnumerable<SourceLabel> labels, IReadOnlyList<AssignmentTarget> targets, ElevationDatum datum,
            double absoluteThreshold = ElevationLabelParser.DefaultAbsoluteThreshold, double conflictTolerance = 0.005)
        {
            var result = new List<AssignmentProposal>();
            foreach (SourceLabel label in labels)
            {
                var p = new AssignmentProposal { Label = label, Parsed = ElevationLabelParser.Parse(label.Text, absoluteThreshold) };
                result.Add(p);
                if (p.Parsed.Kind == LabelKind.Invalid || p.Parsed.Kind == LabelKind.Ambiguous)
                {
                    p.Status = ProposalStatus.BadLabel;
                    p.Message = p.Parsed.Problem ?? Bs.T("Oznaka kote nije upotrebljiva.");
                    continue;
                }

                try
                {
                    double? rel = p.Parsed.Relative;
                    if (p.Parsed.Absolute.HasValue)
                    {
                        if (!datum.IsDefined)
                        {
                            p.Status = ProposalStatus.BadLabel;
                            p.Message = Bs.T("Apsolutna kota {0} ne može se pretvoriti jer nulta kota objekta nije definisana.", ElevationDatum.FormatAbsolute(p.Parsed.Absolute.Value));
                            continue;
                        }

                        double fromAbs = datum.ToRelative(p.Parsed.Absolute.Value);
                        if (rel.HasValue && Math.Abs(rel.Value - fromAbs) > conflictTolerance)
                        {
                            p.Status = ProposalStatus.Conflict;
                            p.Message = Bs.T("Oznaka navodi {0} i {1}, a prema nultoj koti {2} apsolutna kota odgovara {3}.",
                                ElevationDatum.FormatRelative(rel.Value), ElevationDatum.FormatAbsolute(p.Parsed.Absolute.Value),
                                ElevationDatum.FormatAbsolute(datum.BuildingZeroAbsolute!.Value), ElevationDatum.FormatRelative(fromAbs));
                            continue;
                        }

                        rel = fromAbs;
                    }

                    p.RelativeElevation = rel;
                }
                catch (DatumNotDefinedException ex)
                {
                    p.Status = ProposalStatus.BadLabel;
                    p.Message = ex.Message;
                    continue;
                }

                Vec2 at = (label.Target ?? label.Position).XY;
                List<AssignmentTarget> containing = targets.Where(t => t.Closed && GeometryMath.PointInRing(at, t.Boundary)).OrderBy(t => t.Area).ToList();
                if (containing.Count == 0)
                {
                    p.Status = ProposalStatus.NoTarget;
                    p.Message = Bs.T("Oznaka kote ne leži unutar nijednog zatvorenog objekta; odaberite objekat ručno.");
                    continue;
                }

                if (containing.Count > 1 && Math.Abs(containing[0].Area - containing[1].Area) <= Math.Max(1e-6, containing[0].Area * 1e-6))
                {
                    p.Status = ProposalStatus.AmbiguousTarget;
                    p.Message = Bs.T("Oznaka kote leži u više jednakih objekata ({0}); odaberite objekat ručno.", containing.Count);
                    continue;
                }

                p.Target = containing[0];
                p.Status = ProposalStatus.Proposed;
                p.Message = Bs.T("{0} → {1}", ElevationDatum.FormatRelative(p.RelativeElevation!.Value), p.Target.Description);
            }

            // two labels proposing different levels for the same object are both blocked
            foreach (IGrouping<string, AssignmentProposal> g in result.Where(r => r.Status == ProposalStatus.Proposed).GroupBy(r => r.Target!.Handle))
            {
                List<double> levels = g.Select(x => x.RelativeElevation!.Value).Distinct().ToList();
                if (levels.Count > 1 && levels.Max() - levels.Min() > conflictTolerance)
                {
                    foreach (AssignmentProposal x in g)
                    {
                        x.Status = ProposalStatus.Conflict;
                        x.Message = Bs.T("Objekat {0} ima više oznaka kota s različitim vrijednostima ({1}); nagib ili kotu odredite ručno.",
                            x.Target!.Description, string.Join(", ", levels.Select(l => ElevationDatum.FormatRelative(l))));
                    }
                }
            }

            return result;
        }

        public static string StatusText(ProposalStatus s)
        {
            switch (s)
            {
                case ProposalStatus.Proposed: return "Prijedlog";
                case ProposalStatus.NoTarget: return "Bez objekta";
                case ProposalStatus.AmbiguousTarget: return "Više objekata";
                case ProposalStatus.BadLabel: return "Neispravna oznaka";
                case ProposalStatus.Conflict: return "Konflikt";
                default: return s.ToString();
            }
        }
    }
}
