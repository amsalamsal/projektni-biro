using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Symbols;
using SiteDesignPro.Core.Validation;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.UI
{
    /// <summary>
    /// Dockable palette: element library (search, category filter, favourites, recent, symbol previews),
    /// properties of the selected element (edit through SDPSETPARAM so edits are undoable), validation results.
    /// Built in code with WPF controls; colours follow the AutoCAD light/dark theme.
    /// </summary>
    internal static class SitePalette
    {
        private static PaletteSet? set;
        private static LibraryView? library;
        private static PropertiesView? properties;
        private static ValidationView? validation;

        public static void Show(string? category = null)
        {
            if (set == null)
            {
                set = new PaletteSet("Vanjsko uređenje PRO", new Guid("4b8d6f8e-2f8a-4e43-9a51-3f0c6d1d7b21"))
                {
                    Style = PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu,
                    MinimumSize = new System.Drawing.Size(300, 400),
                    DockEnabled = DockSides.Left | DockSides.Right,
                };
                library = new LibraryView();
                properties = new PropertiesView();
                validation = new ValidationView();
                set.AddVisual("Biblioteka", library);
                set.AddVisual("Svojstva", properties);
                set.AddVisual("Provjera", validation);
                AcApp.DocumentManager.DocumentActivated += (s, e) => Log.Guard("Osvježavanje palete", () => properties?.Refresh());
            }

            set.Visible = true;
            if (category != null)
            {
                library!.SetCategory(category);
                set.Activate(0);
            }
        }

        public static void ShowValidation(ValidationReport report, IReadOnlyDictionary<string, string> handles)
        {
            Show();
            validation!.Show(report, handles);
            set!.Activate(2);
        }

        public static void SelectionChanged()
        {
            properties?.Refresh();
        }

        internal static void Run(string macro)
        {
            Document? doc = AcApp.DocumentManager.MdiActiveDocument;
            doc?.SendStringToExecute(macro, true, false, true);
        }

        internal static Brush Back => Icons.DarkTheme ? new SolidColorBrush(Color.FromRgb(0x3B, 0x44, 0x53)) : new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));

        internal static Brush Fore => Icons.DarkTheme ? new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)) : new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));

        internal static Brush Muted => new SolidColorBrush(Color.FromRgb(0x8A, 0x92, 0x9E));

        /// <summary>Vector preview of a point type's plan symbol (generated lazily from the Core symbol library).</summary>
        internal static ImageSource? Preview(ElementType t)
        {
            var pen = new Pen(Fore, 0.04);
            var group = new DrawingGroup();
            using (DrawingContext dc = group.Open())
            {
                if (t.Geometry == GeometryKind.Point)
                {
                    SymbolDrawing d = SymbolLibrary.Build(t.Builder.Id, t.CreateDefaultParameters());
                    Box2 b = d.Bounds();
                    double size = Math.Max(Math.Max(b.Width, b.Height), 0.2);
                    pen.Thickness = size / 40;
                    foreach (SymbolPrimitive p in d.Items)
                    {
                        Draw(dc, p, pen);
                    }
                }
                else if (t.Geometry == GeometryKind.Polygon)
                {
                    dc.DrawRectangle(Muted, pen, new Rect(0, 0, 1, 0.7));
                }
                else
                {
                    pen.Thickness = 0.12;
                    dc.DrawLine(pen, new Point(0, 0.5), new Point(1, 0.2));
                }
            }

            var img = new DrawingImage(group);
            img.Freeze();
            return img;
        }

        private static Point P(Vec2 v) => new Point(v.X, -v.Y);

        private static void Draw(DrawingContext dc, SymbolPrimitive p, Pen pen)
        {
            switch (p)
            {
                case SymLine l:
                    dc.DrawLine(pen, P(l.A), P(l.B));
                    break;
                case SymCircle c:
                    dc.DrawEllipse(null, pen, P(c.Center), c.Radius, c.Radius);
                    break;
                case SymArc a:
                    {
                        double sweep = GeometryMath.NormalizeAngle(a.EndAngle - a.StartAngle);
                        var geo = new StreamGeometry();
                        using (StreamGeometryContext g = geo.Open())
                        {
                            g.BeginFigure(P(a.Center + (new Vec2(Math.Cos(a.StartAngle), Math.Sin(a.StartAngle)) * a.Radius)), false, false);
                            // y is flipped for screen coordinates, so counter-clockwise becomes clockwise
                            g.ArcTo(P(a.Center + (new Vec2(Math.Cos(a.EndAngle), Math.Sin(a.EndAngle)) * a.Radius)), new Size(a.Radius, a.Radius), 0, sweep > Math.PI, SweepDirection.Clockwise, true, false);
                        }

                        dc.DrawGeometry(null, pen, geo);
                        break;
                    }

                case SymPolyline pl:
                case SymFill:
                    {
                        IReadOnlyList<Vec2> pts = p is SymPolyline x
                            ? (x.Bulges == null ? x.Points : GeometryMath.Tessellate(new Ring(x.Points.Select(v => v.WithZ(0)).ToList(), x.Bulges), x.Closed, 0.01).Select(v => v.XY).ToList())
                            : ((SymFill)p).Points;
                        var geo = new StreamGeometry();
                        using (StreamGeometryContext g = geo.Open())
                        {
                            g.BeginFigure(P(pts[0]), p is SymFill, p is SymFill || ((SymPolyline)p).Closed);
                            g.PolyLineTo(pts.Skip(1).Select(P).ToList(), true, false);
                        }

                        dc.DrawGeometry(p is SymFill ? pen.Brush : null, pen, geo);
                        break;
                    }
            }
        }

        internal static TextBlock Label(string text, double size = 12, bool muted = false) =>
            new TextBlock { Text = text, FontSize = size, Foreground = muted ? Muted : Fore, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 1) };
    }

    /// <summary>Searchable element library.</summary>
    internal sealed class LibraryView : DockPanel
    {
        private readonly TextBox search = new TextBox { Margin = new Thickness(4), Padding = new Thickness(3) };
        private readonly ComboBox category = new ComboBox { Margin = new Thickness(4, 0, 4, 4) };
        private readonly ListBox list = new ListBox { BorderThickness = new Thickness(0) };
        private readonly Dictionary<string, ImageSource?> previews = new Dictionary<string, ImageSource?>(StringComparer.Ordinal);

        public LibraryView()
        {
            Background = SitePalette.Back;
            var top = new StackPanel();
            top.Children.Add(search);
            category.Items.Add(new ComboBoxItem { Content = "Sve kategorije", Tag = string.Empty });
            category.Items.Add(new ComboBoxItem { Content = "Omiljeni", Tag = "*fav" });
            category.Items.Add(new ComboBoxItem { Content = "Nedavno korišteni", Tag = "*recent" });
            foreach (CategoryDefinition c in AcadEnv.Catalog.Categories)
            {
                category.Items.Add(new ComboBoxItem { Content = c.Name, Tag = c.Id });
            }

            category.SelectedIndex = 0;
            top.Children.Add(category);
            SetDock(top, Dock.Top);
            Children.Add(top);
            var hint = SitePalette.Label("Dvostruki klik: postavi ili nacrtaj. Desni klik: dodaj ili ukloni iz omiljenih. Pretraga radi i s engleskim nazivima.", 11, true);
            hint.Margin = new Thickness(6, 2, 6, 6);
            hint.TextWrapping = TextWrapping.Wrap;
            SetDock(hint, Dock.Bottom);
            Children.Add(hint);
            list.Background = SitePalette.Back;
            VirtualizingPanel.SetIsVirtualizing(list, true);
            Children.Add(list);
            search.TextChanged += (s, e) => Log.Guard("Pretraga palete", Fill);
            category.SelectionChanged += (s, e) => Log.Guard("Pretraga palete", Fill);
            list.MouseDoubleClick += (s, e) => Log.Guard("Postavljanje iz palete", PlaceSelected);
            list.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Log.Guard("Postavljanje iz palete", PlaceSelected);
                }
            };
            list.MouseRightButtonUp += (s, e) =>
            {
                if (list.SelectedItem is ListBoxItem item && item.Tag is string id)
                {
                    Log.Guard("Omiljeni elementi", () =>
                    {
                        Preferences.Current.ToggleFavorite(id);
                        Fill();
                    });
                }
            };
            Fill();
        }

        public void SetCategory(string id)
        {
            foreach (ComboBoxItem i in category.Items)
            {
                if ((string)i.Tag == id)
                {
                    category.SelectedItem = i;
                }
            }
        }

        private void Fill()
        {
            string q = search.Text.Trim();
            string cat = (category.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
            Preferences prefs = Preferences.Current;
            IEnumerable<ElementType> types = AcadEnv.Catalog.Types;
            if (cat == "*fav")
            {
                types = types.Where(t => prefs.Favorites.Contains(t.Id));
            }
            else if (cat == "*recent")
            {
                types = prefs.Recent.Select(id => AcadEnv.Catalog.FindType(id)).Where(t => t != null).Select(t => t!);
            }
            else if (cat.Length > 0)
            {
                types = types.Where(t => t.Category == cat || t.ExtraPanels.Contains(AcadEnv.Catalog.FindCategory(cat)?.Panel ?? "~"));
            }

            if (q.Length > 0)
            {
                types = types.Where(t => (t.Name + " " + t.Id + " " + t.Subcategory).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            list.Items.Clear();
            foreach (ElementType t in types.Take(400))
            {
                list.Items.Add(Row(t, prefs.Favorites.Contains(t.Id)));
            }
        }

        private ListBoxItem Row(ElementType t, bool favourite)
        {
            if (!previews.TryGetValue(t.Id, out ImageSource? img))
            {
                img = SitePalette.Preview(t);
                previews[t.Id] = img;
            }

            var grid = new Grid { Margin = new Thickness(2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var image = new System.Windows.Controls.Image { Source = img, Width = 32, Height = 32, Stretch = Stretch.Uniform, Margin = new Thickness(2) };
            grid.Children.Add(image);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(SitePalette.Label((favourite ? "★ " : string.Empty) + t.Name));
            text.Children.Add(SitePalette.Label(t.Subcategory + " · " + t.Id, 10, true));
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            return new ListBoxItem { Content = grid, Tag = t.Id, ToolTip = t.Builder.Description };
        }

        private void PlaceSelected()
        {
            if (list.SelectedItem is ListBoxItem item && item.Tag is string id)
            {
                ElementType t = AcadEnv.Catalog.RequireType(id);
                Preferences.Current.Used(id);
                SitePalette.Run((t.Geometry == GeometryKind.Point ? "_SDPPLACE " : "_SDPDRAW ") + id + "\n");
            }
        }
    }

    /// <summary>Parameters of the selected element; changes are applied with SDPSETPARAM (undoable).</summary>
    internal sealed class PropertiesView : DockPanel
    {
        private readonly StackPanel body = new StackPanel { Margin = new Thickness(6) };
        private readonly Dictionary<string, FrameworkElement> editors = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> original = new Dictionary<string, string>(StringComparer.Ordinal);
        private string handles = string.Empty;

        public PropertiesView()
        {
            Background = SitePalette.Back;
            var apply = new Button { Content = "Primijeni izmjene", Margin = new Thickness(6), Padding = new Thickness(8, 3, 8, 3) };
            apply.Click += (s, e) => Log.Guard("Primjena parametara", Apply);
            SetDock(apply, Dock.Bottom);
            Children.Add(apply);
            Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Refresh();
        }

        public void Refresh()
        {
            body.Children.Clear();
            editors.Clear();
            original.Clear();
            handles = string.Empty;
            Document? doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            PromptSelectionResult sel = doc.Editor.SelectImplied();
            if (sel.Status != PromptStatus.OK)
            {
                body.Children.Add(SitePalette.Label("Odaberite elemente u crtežu da vidite njihove parametre.", 12, true));
                return;
            }

            using Transaction tr = doc.TransactionManager.StartOpenCloseTransaction();
            ProjectSettings settings = ProjectStore.ReadOrDefault(doc.Database, tr);
            var svc = new ElementService(doc.Database, tr, AcadEnv.Catalog, settings);
            var managed = new List<ManagedElement>();
            foreach (ObjectId id in sel.Value.GetObjectIds())
            {
                if (tr.GetObject(id, OpenMode.ForRead) is Entity e && ElementStore.IsManaged(e) && svc.Read(e) is ManagedElement m && m.Element != null && m.Type != null)
                {
                    managed.Add(m);
                }
            }

            tr.Commit();
            if (managed.Count == 0)
            {
                body.Children.Add(SitePalette.Label("Odabir ne sadrži elemente Vanjsko uređenje PRO.", 12, true));
                return;
            }

            ElementType type = managed[0].Type!;
            managed = managed.Where(m => m.Type!.Id == type.Id).ToList();
            handles = string.Join(",", managed.Select(m => m.ObjectId.Handle.ToString()));
            body.Children.Add(SitePalette.Label(managed.Count == 1 ? (managed[0].Element!.Label + " " + type.Name) : managed.Count + " × " + type.Name, 14));
            body.Children.Add(SitePalette.Label(type.Id + (managed.Count == 1 ? " · " + managed[0].Element!.Id : string.Empty), 10, true));
            foreach (IGrouping<string, ParameterDefinition> g in type.Parameters.GroupBy(p => p.Group))
            {
                TextBlock header = SitePalette.Label(g.Key.ToUpperInvariant(), 10, true);
                header.Margin = new Thickness(0, 10, 0, 2);
                body.Children.Add(header);
                foreach (ParameterDefinition p in g)
                {
                    List<string> values = managed.Select(m => Display(p, (m.Element!.Parameters.Contains(p.Key) ? m.Element.Parameters[p.Key] : p.Default).AsText())).Distinct().ToList();
                    string value = values.Count == 1 ? values[0] : string.Empty;
                    original[p.Key] = value;
                    var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                    TextBlock lbl = SitePalette.Label(p.Label, 11);
                    lbl.ToolTip = p.Key;
                    row.Children.Add(lbl);
                    FrameworkElement editor = Editor(p, value, values.Count > 1);
                    Grid.SetColumn(editor, 1);
                    row.Children.Add(editor);
                    TextBlock unit = SitePalette.Label(p.DisplayUnit, 10, true);
                    unit.Margin = new Thickness(4, 0, 0, 0);
                    Grid.SetColumn(unit, 2);
                    row.Children.Add(unit);
                    editors[p.Key] = editor;
                    body.Children.Add(row);
                }
            }
        }

        /// <summary>Numbers are shown with a decimal comma; SDPSETPARAM accepts both comma and point.</summary>
        private static string Display(ParameterDefinition p, string raw) => p.IsNumeric ? raw.Replace('.', ',') : raw;

        private static FrameworkElement Editor(ParameterDefinition p, string value, bool mixed)
        {
            switch (p.Type)
            {
                case ParameterType.Bool:
                    return new CheckBox { IsChecked = mixed ? (bool?)null : value == "true", IsThreeState = mixed, VerticalAlignment = VerticalAlignment.Center };
                case ParameterType.Enum:
                    {
                        var cb = new ComboBox();
                        foreach (string o in p.Options)
                        {
                            cb.Items.Add(new ComboBoxItem { Content = AcadEnv.Catalog.OptionLabel(o), Tag = o });
                        }

                        cb.SelectedItem = mixed ? null : cb.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == value);
                        return cb;
                    }

                case ParameterType.Material:
                    {
                        var cb = new ComboBox();
                        foreach (MaterialDefinition m in AcadEnv.Catalog.Materials)
                        {
                            cb.Items.Add(new ComboBoxItem { Content = m.Name, Tag = m.Key });
                        }

                        cb.SelectedItem = cb.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == value);
                        return cb;
                    }

                default:
                    return new TextBox { Text = mixed ? string.Empty : value, ToolTip = mixed ? "Vrijednosti se razlikuju" : (p.Min.HasValue ? "od " + Display(p, p.Min.Value.ToString(CultureInfo.InvariantCulture)) + " do " + (p.Max.HasValue ? Display(p, p.Max.Value.ToString(CultureInfo.InvariantCulture)) : "-") : null) };
            }
        }

        private void Apply()
        {
            if (handles.Length == 0)
            {
                return;
            }

            var macro = new System.Text.StringBuilder();
            foreach (KeyValuePair<string, FrameworkElement> kv in editors)
            {
                string? value = kv.Value switch
                {
                    CheckBox c => c.IsChecked.HasValue ? (c.IsChecked.Value ? "true" : "false") : null,
                    ComboBox c when c.SelectedItem is ComboBoxItem i => (string)i.Tag,
                    ComboBox c => c.SelectedItem as string,
                    TextBox t => t.Text,
                    _ => null,
                };
                if (value == null || value == original[kv.Key] || (kv.Value is TextBox && value.Length == 0 && original[kv.Key].Length == 0))
                {
                    continue;
                }

                macro.Append("_SDPSETPARAM ").Append(handles).Append('\n').Append(kv.Key).Append('\n').Append(value.Length == 0 ? "-" : value).Append('\n');
            }

            if (macro.Length > 0)
            {
                SitePalette.Run(macro.ToString());
            }
        }
    }

    /// <summary>Validation results; double-click zooms to the element.</summary>
    internal sealed class ValidationView : DockPanel
    {
        private readonly ListBox list = new ListBox { BorderThickness = new Thickness(0) };
        private readonly TextBlock summary = SitePalette.Label("Pokrenite SDPVALIDATE ili SDPPROVJERADWG za provjeru crteža.", 12, true);

        public ValidationView()
        {
            Background = SitePalette.Back;
            summary.Margin = new Thickness(6);
            summary.TextWrapping = TextWrapping.Wrap;
            SetDock(summary, Dock.Top);
            Children.Add(summary);
            list.Background = SitePalette.Back;
            Children.Add(list);
            list.MouseDoubleClick += (s, e) =>
            {
                if (list.SelectedItem is ListBoxItem i && i.Tag is string handle)
                {
                    SitePalette.Run("_SDPZOOM " + handle + "\n");
                }
            };
        }

        public void Show(ValidationReport report, IReadOnlyDictionary<string, string> handles)
        {
            summary.Text = string.Format(CultureInfo.InvariantCulture, "{0} objekata: grešaka {1}, upozorenja {2}, za stručni pregled {3}. {4}",
                report.ElementCount, report.ErrorCount, report.WarningCount, report.ReviewCount, ValidationReport.Disclaimer);
            list.Items.Clear();
            foreach (ValidationIssue i in report.Issues.OrderBy(i => i.Severity))
            {
                Color c = i.Severity switch
                {
                    Severity.Error => Color.FromRgb(0xD9, 0x3B, 0x2B),
                    Severity.Warning => Color.FromRgb(0xC8, 0x8A, 0x10),
                    Severity.Review => Color.FromRgb(0x7C, 0x5C, 0xD6),
                    _ => Color.FromRgb(0x88, 0x88, 0x88),
                };
                var panel = new StackPanel { Margin = new Thickness(2, 3, 2, 3) };
                var head = new TextBlock { FontSize = 11, Foreground = SitePalette.Fore };
                head.Inlines.Add(new System.Windows.Documents.Run(ValidationReport.SeverityName(i.Severity).ToUpperInvariant() + "  ") { Foreground = new SolidColorBrush(c), FontWeight = FontWeights.SemiBold });
                head.Inlines.Add(new System.Windows.Documents.Run(i.Code + "  " + (i.Label ?? i.ElementId ?? "crtež")));
                panel.Children.Add(head);
                TextBlock msg = SitePalette.Label(i.Message, 11);
                msg.TextWrapping = TextWrapping.Wrap;
                panel.Children.Add(msg);
                TextBlock res = SitePalette.Label(i.Resolution, 10, true);
                res.TextWrapping = TextWrapping.Wrap;
                panel.Children.Add(res);
                string? handle = i.ElementId != null && handles.TryGetValue(i.ElementId, out string? h) ? h : null;
                list.Items.Add(new ListBoxItem { Content = panel, Tag = handle, ToolTip = handle != null ? "Dvostruki klik: zumiranje na objekat" : null });
            }
        }
    }

    /// <summary>Palette commands.</summary>
    public sealed class PaletteCommands
    {
        [CommandMethod("SITEDESIGNPRO", "SDPBROWSER", CommandFlags.Modal)]
        public void Browser() => SiteDesignPro.AutoCAD.Commands.CommandContext.Safe("SDPBROWSER", () => SitePalette.Show());

        [CommandMethod("SITEDESIGNPRO", "SDPBROWSECAT", CommandFlags.Modal)]
        public void BrowseCategory()
        {
            SiteDesignPro.AutoCAD.Commands.CommandContext.Safe("SDPBROWSECAT", () =>
            {
                Editor ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
                PromptResult r = ed.GetString(new PromptStringOptions("\nKategorija: ") { AllowSpaces = false });
                if (r.Status == PromptStatus.OK)
                {
                    SitePalette.Show(r.StringResult);
                }
            });
        }
    }
}
