using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.Core.Quantities;

namespace SiteDesignPro.AutoCAD.Drawing
{
    /// <summary>Writes a Core schedule as a native AutoCAD table (title row, header row, data rows, totals).</summary>
    internal static class ScheduleTables
    {
        public static ObjectId Insert(Database db, Transaction tr, ElementService svc, ScheduleTable schedule, Point3d at, double textHeight)
        {
            int cols = schedule.Columns.Count;
            int rows = 2 + schedule.Rows.Count + schedule.Totals.Count;
            var table = new Table();
            table.SetDatabaseDefaults(db);
            table.TableStyle = db.Tablestyle;
            table.Position = at;
            table.SetSize(rows, cols);
            table.Layer = svc.Layers.Ensure("anno_schedules");

            table.Cells[0, 0].TextString = schedule.Title;
            table.MergeCells(CellRange.Create(table, 0, 0, 0, cols - 1));
            for (int c = 0; c < cols; c++)
            {
                table.Cells[1, c].TextString = schedule.Columns[c].Title;
            }

            for (int r = 0; r < schedule.Rows.Count; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    table.Cells[r + 2, c].TextString = schedule.Rows[r].Cells[c];
                    if (schedule.Columns[c].Numeric)
                    {
                        table.Cells[r + 2, c].Alignment = CellAlignment.MiddleRight;
                    }
                }
            }

            int tr0 = 2 + schedule.Rows.Count;
            int i = 0;
            foreach (var total in schedule.Totals)
            {
                int q = schedule.Columns.FindIndex(x => x.Key == "quantity");
                int u = schedule.Columns.FindIndex(x => x.Key == "unit");
                table.Cells[tr0 + i, 0].TextString = "Total";
                if (q >= 0)
                {
                    table.Cells[tr0 + i, q].TextString = total.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                }

                if (u >= 0)
                {
                    table.Cells[tr0 + i, u].TextString = total.Key;
                }

                i++;
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    table.Cells[r, c].TextHeight = r == 0 ? textHeight * 1.3 : textHeight;
                }

                table.Rows[r].Height = textHeight * 2.2;
            }

            for (int c = 0; c < cols; c++)
            {
                int longest = Enumerable.Range(0, rows).Max(r => (table.Cells[r, c].TextString ?? string.Empty).Length);
                table.Columns[c].Width = Math.Max(textHeight * 6, Math.Min(textHeight * 40, longest * textHeight * 0.75 + textHeight * 2));
            }

            table.GenerateLayout();
            BlockTableRecord ms = svc.ModelSpace(OpenMode.ForWrite);
            ObjectId id = ms.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);
            return id;
        }
    }
}
