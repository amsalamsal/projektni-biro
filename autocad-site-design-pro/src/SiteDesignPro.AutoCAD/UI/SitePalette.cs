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
        private static FirstStepsView? firstSteps;
        private static Document? hooked;

        /// <summary>Set after a validation in this session; shown as done in the first-steps checklist.</summary>
        internal static bool Validated { get; private set; }

        /// <summary>Set after a package export in this session.</summary>
        internal static bool Exported { get; private set; }

        internal static void MarkExported()
        {
            Exported = true;
            RefreshFirstSteps();
        }

        public static void ShowFirstSteps()
        {
            Show();
            firstSteps!.Refresh();
            set!.Activate(3);
        }

        internal static void RefreshFirstSteps()
        {
            if (set != null && set.Visible)
            {
                Log.Guard("Prvi koraci", () => firstSteps?.Refresh());
            }
        }

        private static void Hook(Document? doc)
        {
            if (doc == null || ReferenceEquals(doc, hooked))
            {
                return;
            }

            if (hooked != null)
            {
                hooked.CommandEnded -= OnCommandEnded;
            }

            hooked = doc;
            doc.CommandEnded += OnCommandEnded;
        }

        private static void OnCommandEnded(object sender, CommandEventArgs e)
        {
            if (e.GlobalCommandName.StartsWith("SDP", StringComparison.OrdinalIgnoreCase) || e.GlobalCommandName == "ERASE" || e.GlobalCommandName == "U" || e.GlobalCommandName == "UNDO")
            {
                RefreshFirstSteps();
            }
        }

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
                firstSteps = new FirstStepsView();
                set.AddVisual("Prvi koraci", firstSteps);
                AcApp.DocumentManager.DocumentActivated += (s, e) => Log.Guard("Osvježavanje palete", () =>
                {
                    properties?.Refresh();
                    Hook(e.Document);
                    RefreshFirstSteps();
                });
            }

            Hook(AcApp.DocumentManager.MdiActiveDocument);
            bool firstUse = Preferences.Current.PaletteLayout < 1;
            set.Visible = true;

            // On first use AutoCAD floats a new palette as a small window in the top-left corner, over the ribbon
            // buttons. Dock it on the right once; a size or position the user chooses afterwards is kept by AutoCAD.
            if (Preferences.Current.PaletteLayout < 1)
            {
                if (set.Dock == DockSides.None)
                {
                    set.Dock = DockSides.Right;
                }

                Preferences.Current.PaletteLayout = 1;
                Preferences.Current.Save();
            }

            if (category != null)
            {
                library!.SetCategory(category);
                set.Activate(0);
            }
            else if (firstUse)
            {
                firstSteps!.Refresh();
                set.Activate(3);
            }
        }

        public static void ShowValidation(ValidationReport report, IReadOnlyDictionary<string, string> handles)
        {
            Show();
            Validated = true;
            validation!.Show(report, handles);
            set!.Activate(2);
        }

        public static void SelectionChanged()
        {
            properties?.Refresh();
        }

        internal static void Run(string macro, string? typeHint = null)
        {
            Document? doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            if (typeHint != null)
            {
                SiteDesignPro.AutoCAD.Commands.CommandContext.TypeHint = typeHint;
            }

            doc.SendStringToExecute(macro, true, false, true);

            // A click on the ribbon or the palette leaves keyboard focus there, so answers typed at the command's
            // prompts would not reach the command line. Hand focus back to the drawing, as AutoCAD's own buttons do.
            try
            {
                Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            }
            catch (System.Exception)
            {
                // Focus is a convenience; the command itself has already been queued.
            }
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
                SitePalette.Run(t.Geometry == GeometryKind.Point ? "_SDPPLACE " : "_SDPDRAW ", id);
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
            var apply = new Button { Content = "Primijeni izmjene", Margin = new Thickness(6), Padding = new Thickness(8, 3, 8, 3), ToolTip = "Izmjene se primjenjuju i same: Enter, izlazak iz polja ili izbor s liste." };
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
                    HookApply(editor);
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

        /// <summary>A changed value is applied at once: Enter or leaving a text field, choosing a list item, ticking a box.</summary>
        private void HookApply(FrameworkElement editor)
        {
            switch (editor)
            {
                case TextBox t:
                    t.KeyDown += (s, e) =>
                    {
                        if (e.Key == Key.Enter)
                        {
                            Log.Guard("Primjena parametara", Apply);
                        }
                    };
                    t.LostKeyboardFocus += (s, e) => Log.Guard("Primjena parametara", Apply);
                    break;
                case ComboBox c:
                    c.SelectionChanged += (s, e) => Log.Guard("Primjena parametara", Apply);
                    break;
                case CheckBox b:
                    b.Click += (s, e) => Log.Guard("Primjena parametara", Apply);
                    break;
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
                original[kv.Key] = value;
            }

            if (macro.Length > 0)
            {
                SitePalette.Run(macro.ToString());
            }
        }
    }

    /// <summary>
    /// Validation results grouped by message: errors and warnings first, items for professional review only on
    /// request. Double-click zooms to the elements of a group; "Odaberi i uredi" opens SDPEDIT for all of them.
    /// </summary>
    internal sealed class ValidationView : DockPanel
    {
        private readonly ListBox list = new ListBox { BorderThickness = new Thickness(0) };
        private readonly TextBlock summary = SitePalette.Label("Pokrenite Provjeri projekat (SDPVALIDATE) ili Provjera crteža (SDPPROVJERADWG).", 12, true);
        private readonly CheckBox showReview = new CheckBox { Margin = new Thickness(6, 0, 6, 6) };
        private ValidationReport? report;
        private IReadOnlyDictionary<string, string> handles = new Dictionary<string, string>();

        public ValidationView()
        {
            Background = SitePalette.Back;
            var top = new StackPanel();
            summary.Margin = new Thickness(6);
            summary.TextWrapping = TextWrapping.Wrap;
            top.Children.Add(summary);
            showReview.Content = SitePalette.Label("Prikaži i stavke za stručni pregled", 11);
            showReview.Click += (s, e) => Log.Guard("Provjera", Render);
            top.Children.Add(showReview);
            SetDock(top, Dock.Top);
            Children.Add(top);
            list.Background = SitePalette.Back;
            Children.Add(list);
            list.MouseDoubleClick += (s, e) =>
            {
                if (list.SelectedItem is ListBoxItem i && i.Tag is string h && h.Length > 0)
                {
                    SitePalette.Run("_SDPZOOM " + h + "\n");
                }
            };
        }

        public void Show(ValidationReport newReport, IReadOnlyDictionary<string, string> newHandles)
        {
            report = newReport;
            handles = newHandles;
            Render();
        }

        private void Render()
        {
            if (report == null)
            {
                return;
            }

            bool review = showReview.IsChecked == true;
            summary.Text = string.Format(CultureInfo.InvariantCulture, "{0} objekata: grešaka {1}, upozorenja {2}, za stručni pregled {3}{4}. {5}",
                report.ElementCount, report.ErrorCount, report.WarningCount, report.ReviewCount, review || report.ReviewCount == 0 ? string.Empty : " (skriveno)", ValidationReport.Disclaimer);
            list.Items.Clear();
            IEnumerable<ValidationIssue> issues = report.Issues.Where(i => review || i.Severity == Severity.Error || i.Severity == Severity.Warning);
            foreach (IGrouping<string, ValidationIssue> g in issues.GroupBy(i => ((int)i.Severity).ToString(CultureInfo.InvariantCulture) + "|" + i.Code + "|" + i.Message).OrderBy(g => g.First().Severity).ThenBy(g => g.First().Code))
            {
                ValidationIssue i = g.First();
                Color c = i.Severity switch
                {
                    Severity.Error => Color.FromRgb(0xD9, 0x3B, 0x2B),
                    Severity.Warning => Color.FromRgb(0xC8, 0x8A, 0x10),
                    Severity.Review => Color.FromRgb(0x7C, 0x5C, 0xD6),
                    _ => Color.FromRgb(0x88, 0x88, 0x88),
                };
                List<string> labels = g.Select(x => x.Label ?? x.ElementId ?? "crtež").Distinct().ToList();
                string groupHandles = string.Join(",", g.Where(x => x.ElementId != null && handles.ContainsKey(x.ElementId)).Select(x => handles[x.ElementId!]).Distinct());
                var panel = new StackPanel { Margin = new Thickness(2, 3, 2, 3) };
                var head = new TextBlock { FontSize = 11, Foreground = SitePalette.Fore, TextWrapping = TextWrapping.Wrap };
                head.Inlines.Add(new System.Windows.Documents.Run(ValidationReport.SeverityName(i.Severity).ToUpperInvariant() + "  ") { Foreground = new SolidColorBrush(c), FontWeight = FontWeights.SemiBold });
                head.Inlines.Add(new System.Windows.Documents.Run(i.Code + "  " + (labels.Count > 1 ? "×" + labels.Count + "  " : string.Empty) + string.Join(", ", labels.Take(6)) + (labels.Count > 6 ? ", …" : string.Empty)));
                panel.Children.Add(head);
                TextBlock msg = SitePalette.Label(i.Message, 11);
                msg.TextWrapping = TextWrapping.Wrap;
                panel.Children.Add(msg);
                TextBlock res = SitePalette.Label(i.Resolution, 10, true);
                res.TextWrapping = TextWrapping.Wrap;
                panel.Children.Add(res);
                if (groupHandles.Length > 0)
                {
                    var edit = new Button { Content = labels.Count > 1 ? "Odaberi svih " + labels.Count + " i uredi" : "Odaberi i uredi", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 3, 0, 0), Padding = new Thickness(6, 1, 6, 1), FontSize = 11 };
                    edit.Click += (s, e) => SitePalette.Run("_SDPZOOM " + groupHandles + "\n_SDPEDIT ");
                    panel.Children.Add(edit);
                }

                list.Items.Add(new ListBoxItem { Content = panel, Tag = groupHandles, ToolTip = groupHandles.Length > 0 ? "Dvostruki klik: zumiranje na objekte" : null });
            }

            if (list.Items.Count == 0)
            {
                list.Items.Add(new ListBoxItem { Content = SitePalette.Label(report.ErrorCount + report.WarningCount == 0 ? "Nema grešaka ni upozorenja." : "Nema stavki.", 12, true) });
            }
        }
    }

    /// <summary>
    /// "Prvi koraci": the work order of a project as a checklist. Each step starts its command; a step is ticked
    /// when the drawing shows it is done.
    /// </summary>
    internal sealed class FirstStepsView : DockPanel
    {
        private readonly StackPanel body = new StackPanel { Margin = new Thickness(6) };

        public FirstStepsView()
        {
            Background = SitePalette.Back;
            var refresh = new Button { Content = "Osvježi", Margin = new Thickness(6), Padding = new Thickness(8, 2, 8, 2), HorizontalAlignment = HorizontalAlignment.Right };
            refresh.Click += (s, e) => Log.Guard("Prvi koraci", Refresh);
            SetDock(refresh, Dock.Bottom);
            Children.Add(refresh);
            Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        }

        public void Refresh()
        {
            body.Children.Clear();
            Document? doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            bool project, zero = false, site = false, roads = false, content = false, sheet = false;
            using (Transaction tr = doc.TransactionManager.StartOpenCloseTransaction())
            {
                ProjectSettings? stored = ProjectStore.Read(doc.Database, tr);
                project = stored != null;
                zero = stored?.Elevations.BuildingZeroAbsolute.HasValue == true;
                var svc = new ElementService(doc.Database, tr, AcadEnv.Catalog, stored ?? new ProjectSettings());
                foreach (ManagedElement m in svc.ReadAll())
                {
                    string cat = m.Type?.Category ?? string.Empty;
                    site |= m.Record.TypeId == "site.boundary";
                    roads |= cat == "roads" || cat == "cycling";
                    content |= cat == "buildings" || cat == "surfaces" || cat == "landscape" || cat == "furniture" || cat == "lighting" || cat == "recreation";
                }

                sheet = svc.Aux("sheet").Any();
                tr.Commit();
            }

            TextBlock intro = SitePalette.Label("Redoslijed rada. Kliknite korak da ga pokrenete; urađeni koraci se sami štikliraju.", 11, true);
            intro.TextWrapping = TextWrapping.Wrap;
            intro.Margin = new Thickness(0, 0, 0, 8);
            body.Children.Add(intro);
            Step(1, "Novi projekat", "Naziv, jedinice, mjerilo i nulta kota u jednom prozoru.", "_SDPNEW ", project);
            Step(2, "Nulta kota ±0,00", "Apsolutna visina poda prizemlja (može i kasnije).", "_SDPDATUM ", zero);
            Step(3, "Parcela", "Nacrtajte granicu parcele (pravougaonik ili polilinija).", "_SDPDRAW ", site, "site.boundary");
            Step(4, "Ceste, staze, kružni tokovi", "Ulica po profilu iz jedne osovine; kružni tok iz jedne tačke.", "_SDPULICA ", roads);
            Step(5, "Objekti, parter, zelenilo, oprema", "Iz biblioteke: dvostruki klik na element.", "_SDPBROWSER ", content);
            Step(6, "Dokumentuj list", "Oznake, legenda, tabele, sjever, razmjernik i okvir lista odjednom.", "_SDPDOKUMENTUJ ", sheet);
            Step(7, "Provjeri projekat", "Kolizije, izlazak iz parcele, podaci.", "_SDPVALIDATE ", SitePalette.Validated);
            Step(8, "Izvoz za SketchUp", "Paket .sdpkg za 3D model.", "_SDPEXPORT ", SitePalette.Exported);
        }

        private void Step(int n, string title, string hint, string macro, bool done, string? typeHint = null)
        {
            var b = new Button
            {
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(6, 4, 6, 4),
                Background = SitePalette.Back,
                BorderBrush = new SolidColorBrush(done ? Color.FromRgb(0x4E, 0x9A, 0x49) : Color.FromRgb(0x6A, 0x72, 0x7E)),
            };
            var row = new DockPanel();
            var mark = new TextBlock
            {
                Text = done ? "✔" : n.ToString(CultureInfo.InvariantCulture),
                Width = 22,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = done ? new SolidColorBrush(Color.FromRgb(0x5D, 0xB8, 0x57)) : SitePalette.Fore,
                VerticalAlignment = VerticalAlignment.Top,
            };
            SetDock(mark, Dock.Left);
            row.Children.Add(mark);
            var text = new StackPanel();
            text.Children.Add(SitePalette.Label(title, 12));
            TextBlock h = SitePalette.Label(hint, 10, true);
            h.TextWrapping = TextWrapping.Wrap;
            text.Children.Add(h);
            row.Children.Add(text);
            b.Content = row;
            b.Click += (s, e) => SitePalette.Run(macro, typeHint);
            body.Children.Add(b);
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
