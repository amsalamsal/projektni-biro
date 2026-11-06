using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Symbols;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    /// <summary>
    /// Writes every point type's plan symbol as JSON (primitives in metres) when SDP_SYMBOL_PREVIEW names a file,
    /// so the symbols can be reviewed as a picture without AutoCAD. Without the variable the test only builds them.
    /// </summary>
    public class SymbolPreviewTests
    {
        [Fact]
        public void AllPointSymbols_CanBeExportedForReview()
        {
            string? path = Environment.GetEnvironmentVariable("SDP_SYMBOL_PREVIEW");
            var sb = new StringBuilder("[");
            bool first = true;
            string N(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
            string V(Vec2 v) => "[" + N(v.X) + "," + N(v.Y) + "]";
            string S(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            foreach (ElementType t in TestSupport.Catalog.Types.Where(t => t.Geometry == GeometryKind.Point))
            {
                SymbolDrawing d = SymbolLibrary.Build(t.Builder.Id, t.CreateDefaultParameters(), k => t.Parameter(k)?.Default.AsNumber() ?? double.NaN, t.Id);
                Assert.NotEmpty(d.Items);
                sb.Append(first ? string.Empty : ",").Append("{\"id\":").Append(S(t.Id)).Append(",\"name\":").Append(S(t.Name))
                  .Append(",\"category\":").Append(S(t.Category)).Append(",\"symbol\":").Append(S(d.Symbol)).Append(",\"items\":[");
                first = false;
                sb.Append(string.Join(",", d.Items.Select(p => p switch
                {
                    SymLine l => "{\"t\":\"L\",\"a\":" + V(l.A) + ",\"b\":" + V(l.B) + "}",
                    SymCircle c => "{\"t\":\"C\",\"c\":" + V(c.Center) + ",\"r\":" + N(c.Radius) + "}",
                    SymArc a => "{\"t\":\"A\",\"c\":" + V(a.Center) + ",\"r\":" + N(a.Radius) + ",\"s\":" + N(a.StartAngle) + ",\"e\":" + N(a.EndAngle) + "}",
                    SymPolyline pl => "{\"t\":\"P\",\"closed\":" + (pl.Closed ? "true" : "false") + ",\"pts\":[" + string.Join(",", pl.Points.Select(V)) + "],\"bulges\":[" + string.Join(",", (pl.Bulges ?? Array.Empty<double>()).Select(N)) + "]}",
                    SymFill f => "{\"t\":\"F\",\"pts\":[" + string.Join(",", f.Points.Select(V)) + "]}",
                    SymText x => "{\"t\":\"T\",\"p\":" + V(x.Position) + ",\"h\":" + N(x.Height) + ",\"s\":" + S(x.Text) + "}",
                    _ => "{}",
                })));
                sb.Append("]}");
            }

            sb.Append("]");
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
        }
    }
}
