using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Windows;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Catalog;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.UI
{
    /// <summary>Executes the command string stored in a ribbon item's CommandParameter.</summary>
    internal sealed class RibbonCommandHandler : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            string? macro = (parameter as RibbonCommandItem)?.CommandParameter as string;
            Document? doc = AcApp.DocumentManager.MdiActiveDocument;
            if (macro != null && doc != null)
            {
                doc.SendStringToExecute(macro, true, false, true);
            }
        }
    }

    /// <summary>
    /// Builds the SITE DESIGN PRO ribbon tab. Every button runs a real command; element-type buttons are
    /// generated from the catalog (so user catalog types appear automatically).
    /// </summary>
    internal static class RibbonBuilder
    {
        public const string TabId = "SITE_DESIGN_PRO_TAB";
        private static readonly RibbonCommandHandler Handler = new RibbonCommandHandler();

        public static void Create()
        {
            RibbonControl? ribbon = ComponentManager.Ribbon;
            if (ribbon == null)
            {
                return;
            }

            RibbonTab? old = ribbon.Tabs.FirstOrDefault(t => t.Id == TabId);
            if (old != null)
            {
                ribbon.Tabs.Remove(old);
            }

            var tab = new RibbonTab { Title = "SITE DESIGN PRO", Id = TabId };
            ElementCatalog cat = AcadEnv.Catalog;

            tab.Panels.Add(Panel("Project",
                Large("New project", "SDPNEW", "new", "Set up the project: name, units, origin, north, scale."),
                Stack(Small("Settings", "SDPSETTINGS", "settings", "Project settings: units, origin, north, annotation scale, numbering."),
                      Small("Layers", "SDPLAYERS", "layers", "Create, repair or audit the project layer standard."),
                      Small("Audit", "SDPAUDIT", "audit", "Repair identifiers of copied objects, refresh symbols, report problems.")),
                Large("Library", "SDPBROWSER", "browser", "Open the Site Design Pro palette: searchable element library, properties and validation results."),
                Break(),
                Small("Sample project", "SDPSAMPLE", "sample", "Build the sample residential complex in this drawing.")));

            tab.Panels.Add(Panel("Site Analysis",
                Large("Validate", "SDPVALIDATE", "validate", "Check geometry, data, collisions, overlaps, clearances and review items."),
                Stack(Type(cat, "site.boundary", "site"), Type(cat, "building.footprint", "buildings"), Type(cat, "landscape.tree_existing", "landscape")),
                Stack(Small("Clearances", "SDPCLEARANCE", "clearance", "Draw service, safety and lifting clearance envelopes from element values."),
                      Small("Select", "SDPSELECT", "select", "Select elements by type, category, label, id or material."),
                      Small("Quantities", "SDPQUANTITIES", "quantities", "List quantities per element type."))));

            AddCategoryPanel(tab, cat, "surfaces", "Surfaces", Small("Pick area", "SDPDRAW surface.concrete_paving\n", "draw", "Draw a surface from a polyline, rectangle or picked boundary."),
                Small("Convert", "SDPCONVERT", "convert", "Convert existing polylines, lines, arcs or circles into elements."));
            AddCategoryPanel(tab, cat, "roads", "Roads & Mobility", Small("Parking row", "SDPPARKING", "parking", "Generate a row of parking bays along an aisle edge."),
                Small("Width labels", "SDPWIDTHLABEL", "width", "Label road and path widths."));
            AddCategoryPanel(tab, cat, "cycling", "Cycling Infrastructure", Small("Stands along path", "SDPPATHARRAY", "patharray", "Repeat stands or racks along a path."));
            AddCategoryPanel(tab, cat, "water", "Water & Drainage", Small("Slope arrow", "SDPSLOPE", "slope", "Slope arrow with gradient from point elevations."));
            AddCategoryPanel(tab, cat, "landscape", "Landscape", Small("Tree row", "SDPPATHARRAY", "patharray", "Repeat trees along a path at a spacing."),
                Small("Planting grid", "SDPGRID", "plantgrid", "Fill a boundary with a planting grid (staggered or square)."));
            AddCategoryPanel(tab, cat, "utilities", "Utilities");
            AddCategoryPanel(tab, cat, "lighting", "Lighting", Small("Lights along path", "SDPPATHARRAY", "patharray", "Repeat lights along a road or path."),
                Small("Coverage (illustrative)", "SDPLIGHTCOVER", "lightcover", "Illustrative coverage circles; not a photometric calculation."));
            AddCategoryPanel(tab, cat, "walls", "Walls & Fences");
            AddCategoryPanel(tab, cat, "structures", "Structures");
            AddCategoryPanel(tab, cat, "furniture", "Furniture & Recreation", Small("Arrange along path", "SDPPATHARRAY", "patharray", "Arrange benches or furniture along a path."));
            AddCategoryPanel(tab, cat, "sustainability", "Sustainability");
            AddCategoryPanel(tab, cat, "climate", "Climate Systems");
            AddCategoryPanel(tab, cat, "decorative", "Decorative Elements");
            AddCategoryPanel(tab, cat, "information", "Information Systems");

            tab.Panels.Add(Panel("Annotations",
                Large("Label", "SDPLABEL", "label", "Label elements with numbers and key data (multileaders)."),
                Stack(Small("Renumber", "SDPNUMBER", "number", "Assign missing numbers or renumber north to south, west to east."),
                      Small("Area labels", "SDPAREALABEL", "area", "Surface code, type and area."),
                      Small("Spot level", "SDPSPOTLEVEL", "spotlevel", "Spot level from point elevation.")),
                Stack(Small("North arrow", "SDPNORTH", "north", "North arrow using the project true north."),
                      Small("Scale bar", "SDPSCALEBAR", "scalebar", "Scale bar for the annotation scale."),
                      Small("Coordinates", "SDPCOORD", "coord", "Easting / northing marker.")),
                Break(),
                Stack(Small("Section marker", "SDPSECTION", "section", "Section line with markers."),
                      Small("Detail reference", "SDPDETAIL", "detail", "Detail reference bubble."),
                      Small("Grid lines", "SDPGRIDLINES", "grid", "Grid lines with bubbles.")),
                Stack(Small("Legend", "SDPLEGEND", "legend", "Symbol key of the element types in the drawing."),
                      Small("Title block", "SDPTITLEBLOCK", "titleblock", "Sheet border and title block at the project scale."),
                      Small("Revision cloud", "SDPREVCLOUD", "revcloud", "Revision cloud with revision note."))));

            tab.Panels.Add(Panel("Quantities & Schedules",
                Large("Schedule", "SDPSCHEDULE", "schedule", "Insert quantity schedules as AutoCAD tables."),
                Stack(Small("Export CSV", "SDPSCHEDULECSV", "csv", "Write all schedules to CSV and JSON."),
                      Small("Quantities", "SDPQUANTITIES", "quantities", "Quantities per type on the command line."))));

            tab.Panels.Add(Panel("SketchUp Export",
                Large("Export package", "SDPEXPORT", "export", "Validate and write the .sdpkg package for SketchUp."),
                Stack(Small("Validate", "SDPVALIDATE", "validate", "Run all checks before export."),
                      Small("Apply SketchUp changes", "SDPAPPLYCHANGES", "apply", "Apply parameter edits and moved elements from SketchUp."),
                      Small("Edit element", "SDPEDIT", "edit", "Edit parameters, label or properties of selected elements."))));

            tab.Panels.Add(Panel("Settings & Libraries",
                Large("Palette", "SDPBROWSER", "browser", "Element library, properties and validation results."),
                Stack(Small("User catalogs", "SDPCATALOGFOLDER", "catalog", "Open the folder for user element types, layers and materials."),
                      Small("Reload catalog", "SDPRELOAD", "reload", "Reload the catalog including user catalogs."),
                      Small("About", "SDPABOUT", "about", "Version, catalog and log file."))));

            ribbon.Tabs.Add(tab);
            Log.Info("Ribbon tab created with " + tab.Panels.Count + " panels.");
        }

        private static void AddCategoryPanel(RibbonTab tab, ElementCatalog cat, string category, string title, params RibbonItem[] tools)
        {
            List<ElementType> types = cat.Types.Where(t => t.Category == category || t.ExtraPanels.Contains(title)).ToList();
            var items = new List<RibbonItem>
            {
                Large("Browse", "SDPBROWSECAT " + category + "\n", category, "Browse all " + title + " elements in the palette."),
            };
            var splits = types.GroupBy(t => t.Subcategory.Length > 0 ? t.Subcategory : title)
                .Select(g => Split(g.Key, category, g.ToList()))
                .ToList();
            for (int i = 0; i < splits.Count; i += 3)
            {
                items.Add(Stack(splits.Skip(i).Take(3).ToArray()));
                if (i == 0 && splits.Count > 6)
                {
                    // keep the panel compact: the remaining subcategories go to the slide-out
                    items.Add(Break());
                }
            }

            if (tools.Length > 0)
            {
                items.Add(Stack(tools));
            }

            tab.Panels.Add(Panel(title, items.ToArray()));
        }

        private static RibbonPanel Panel(string title, params RibbonItem[] items)
        {
            var source = new RibbonPanelSource { Title = title };
            foreach (RibbonItem i in items)
            {
                source.Items.Add(i);
            }

            return new RibbonPanel { Source = source };
        }

        private static RibbonItem Break() => new RibbonPanelBreak();

        private static RibbonButton Button(string text, string macro, string icon, string tip, RibbonItemSize size)
        {
            string command = macro.EndsWith("\n", StringComparison.Ordinal) ? "_" + macro : "_" + macro + " ";
            return new RibbonButton
            {
                Text = text,
                ShowText = true,
                ShowImage = true,
                Size = size,
                Orientation = size == RibbonItemSize.Large ? System.Windows.Controls.Orientation.Vertical : System.Windows.Controls.Orientation.Horizontal,
                Image = Icons.Get(icon, 16),
                LargeImage = Icons.Get(icon, 32),
                CommandParameter = command,
                CommandHandler = Handler,
                ToolTip = new RibbonToolTip { Title = text, Content = tip, Command = macro.Split(' ', '\n')[0] },
                Id = "SDP_" + macro.Split(' ', '\n')[0] + "_" + text.Replace(" ", string.Empty),
            };
        }

        private static RibbonButton Large(string text, string macro, string icon, string tip) => Button(text, macro, icon, tip, RibbonItemSize.Large);

        private static RibbonButton Small(string text, string macro, string icon, string tip) => Button(text, macro, icon, tip, RibbonItemSize.Standard);

        private static RibbonRowPanel Stack(params RibbonItem[] items)
        {
            var row = new RibbonRowPanel();
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0)
                {
                    row.Items.Add(new RibbonRowBreak());
                }

                row.Items.Add(items[i]);
            }

            return row;
        }

        private static RibbonItem Type(ElementCatalog cat, string typeId, string icon)
        {
            ElementType? t = cat.FindType(typeId);
            if (t == null)
            {
                return Small(typeId, "SDPBROWSER", icon, "Type not found in the catalog.");
            }

            string command = t.Geometry == Core.Geometry.GeometryKind.Point ? "SDPPLACE " : "SDPDRAW ";
            return Small(t.Name, command + t.Id + "\n", icon, PlacementTip(t));
        }

        private static RibbonSplitButton Split(string title, string icon, List<ElementType> types)
        {
            var split = new RibbonSplitButton
            {
                Text = title,
                ShowText = true,
                ShowImage = true,
                Size = RibbonItemSize.Standard,
                Image = Icons.Get(icon, 16),
                LargeImage = Icons.Get(icon, 32),
                IsSplit = true,
                IsSynchronizedWithCurrentItem = true,
                ListStyle = RibbonSplitButtonListStyle.List,
            };
            foreach (ElementType t in types)
            {
                string command = t.Geometry == Core.Geometry.GeometryKind.Point ? "SDPPLACE " : "SDPDRAW ";
                RibbonButton b = Small(t.Name, command + t.Id + "\n", icon, PlacementTip(t));
                split.Items.Add(b);
            }

            return split;
        }

        private static string PlacementTip(ElementType t) => t.Name + " (" + t.Id + "). " + t.Builder.Description +
            (t.Geometry == Core.Geometry.GeometryKind.Point ? " Pick insertion point and rotation." : " Draw a polyline, rectangle, pick a boundary or select existing curves.");
    }
}
