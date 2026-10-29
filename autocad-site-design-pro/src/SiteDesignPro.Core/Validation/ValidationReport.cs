using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace SiteDesignPro.Core.Validation
{
    public enum Severity
    {
        /// <summary>Must be fixed: the element cannot be exported or reconstructed correctly.</summary>
        Error,

        /// <summary>Probable problem that should be fixed; export continues.</summary>
        Warning,

        /// <summary>Requires professional review; not an error and not a compliance statement.</summary>
        Review,

        Info,
    }

    public enum IssueKind
    {
        /// <summary>Definite geometric error (open boundary, self-intersection, zero length ...).</summary>
        GeometricError,

        /// <summary>Invalid, missing or inconsistent data.</summary>
        DataError,

        /// <summary>Potential design conflict (overlap, collision, clearance).</summary>
        DesignConflict,

        /// <summary>Item that needs professional input or verification.</summary>
        ProfessionalReview,

        /// <summary>Package / format / version problem.</summary>
        Format,
    }

    public sealed class ValidationIssue
    {
        public ValidationIssue(Severity severity, IssueKind kind, string code, string? elementId, string message, string resolution)
        {
            Severity = severity;
            Kind = kind;
            Code = code;
            ElementId = elementId;
            Message = message;
            Resolution = resolution;
        }

        public Severity Severity { get; }

        public IssueKind Kind { get; }

        /// <summary>Stable rule code, e.g. SDP-G001.</summary>
        public string Code { get; }

        public string? ElementId { get; }

        /// <summary>Second element involved (collisions, overlaps).</summary>
        public string? RelatedElementId { get; set; }

        public string? Label { get; set; }

        public string Message { get; }

        public string Resolution { get; }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "[{0}] {1} {2}: {3}", Severity, Code, Label ?? ElementId ?? "project", Message);
    }

    public sealed class ValidationReport
    {
        public const string Disclaimer =
            "Automated checks support design coordination only. Clearance, safety, accessibility and capacity checks use " +
            "values entered by the user; they are not certified regulatory compliance checks and do not replace professional review.";

        public List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();

        public int ElementCount { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public int ErrorCount => Issues.Count(i => i.Severity == Severity.Error);

        public int WarningCount => Issues.Count(i => i.Severity == Severity.Warning);

        public int ReviewCount => Issues.Count(i => i.Severity == Severity.Review);

        public bool HasErrors => ErrorCount > 0;

        public void Add(ValidationIssue issue) => Issues.Add(issue);

        public void Add(Severity severity, IssueKind kind, string code, string? elementId, string message, string resolution)
            => Issues.Add(new ValidationIssue(severity, kind, code, elementId, message, resolution));

        public IEnumerable<ValidationIssue> ForElement(string id) => Issues.Where(i => i.ElementId == id || i.RelatedElementId == id);

        public JsonObject ToJson()
        {
            return new JsonObject
            {
                ["createdUtc"] = CreatedUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["elementCount"] = ElementCount,
                ["errors"] = ErrorCount,
                ["warnings"] = WarningCount,
                ["reviews"] = ReviewCount,
                ["disclaimer"] = Disclaimer,
                ["issues"] = new JsonArray(Issues.Select(i => (JsonNode)new JsonObject
                {
                    ["severity"] = i.Severity.ToString().ToLowerInvariant(),
                    ["kind"] = i.Kind.ToString(),
                    ["code"] = i.Code,
                    ["elementId"] = i.ElementId,
                    ["relatedElementId"] = i.RelatedElementId,
                    ["label"] = i.Label,
                    ["message"] = i.Message,
                    ["resolution"] = i.Resolution,
                }).ToArray()),
            };
        }

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Site Design Pro validation report");
            sb.AppendLine("Created (UTC): " + CreatedUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Elements: {0}  Errors: {1}  Warnings: {2}  Reviews: {3}", ElementCount, ErrorCount, WarningCount, ReviewCount));
            sb.AppendLine();
            foreach (IGrouping<Severity, ValidationIssue> g in Issues.OrderBy(i => i.Severity).ThenBy(i => i.Code, StringComparer.Ordinal).GroupBy(i => i.Severity))
            {
                sb.AppendLine(g.Key.ToString().ToUpperInvariant());
                foreach (ValidationIssue i in g)
                {
                    sb.Append("  ").Append(i.Code).Append("  ").Append(i.Label ?? i.ElementId ?? "project").Append(": ").AppendLine(i.Message);
                    sb.Append("      -> ").AppendLine(i.Resolution);
                }

                sb.AppendLine();
            }

            sb.AppendLine(Disclaimer);
            return sb.ToString();
        }
    }
}
