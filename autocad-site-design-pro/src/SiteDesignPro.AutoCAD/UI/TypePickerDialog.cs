using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.UI
{
    /// <summary>
    /// Searchable list of element types, opened wherever a command needs a type and a person is at the keyboard,
    /// so nobody has to know ids such as <c>landscape.lawn_area</c>.
    /// </summary>
    internal sealed class TypePickerDialog : Window
    {
        private readonly TextBox search = new TextBox { Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(4) };
        private readonly ComboBox category = new ComboBox { Margin = new Thickness(0, 0, 0, 6) };
        private readonly ListBox list = new ListBox();
        private readonly GeometryKind[] kinds;
        private readonly Dictionary<string, ImageSource?> previews = new Dictionary<string, ImageSource?>(StringComparer.Ordinal);

        private TypePickerDialog(string title, GeometryKind[] kinds)
        {
            this.kinds = kinds;
            Title = title;
            Width = 460;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.CanResizeWithGrip;

            var root = new DockPanel { Margin = new Thickness(10) };
            var top = new StackPanel();
            top.Children.Add(new TextBlock { Text = "Pretraga (bosanski ili engleski naziv):", Margin = new Thickness(0, 0, 0, 2) });
            top.Children.Add(search);
            category.Items.Add(new ComboBoxItem { Content = "Sve kategorije", Tag = string.Empty });
            category.Items.Add(new ComboBoxItem { Content = "Nedavno korišteni", Tag = "*recent" });
            category.Items.Add(new ComboBoxItem { Content = "Omiljeni", Tag = "*fav" });
            foreach (CategoryDefinition c in AcadEnv.Catalog.Categories)
            {
                category.Items.Add(new ComboBoxItem { Content = c.Name, Tag = c.Id });
            }

            category.SelectedIndex = Preferences.Current.Recent.Count > 0 && search.Text.Length == 0 ? 1 : 0;
            top.Children.Add(category);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            var ok = new Button { Content = "Odaberi", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(8, 3, 8, 3) };
            var cancel = new Button { Content = "Odustani", IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 3, 8, 3) };
            ok.Click += (s, e) => Accept();
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            VirtualizingPanel.SetIsVirtualizing(list, true);
            root.Children.Add(list);
            Content = root;

            search.TextChanged += (s, e) => Log.Guard("Pretraga tipova", Fill);
            category.SelectionChanged += (s, e) => Log.Guard("Pretraga tipova", Fill);
            list.MouseDoubleClick += (s, e) => Accept();
            search.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Down && list.Items.Count > 0)
                {
                    list.SelectedIndex = Math.Min(list.SelectedIndex + 1, list.Items.Count - 1);
                    ((ListBoxItem)list.SelectedItem).Focus();
                    e.Handled = true;
                }
            };
            Loaded += (s, e) =>
            {
                search.Focus();
                Keyboard.Focus(search);
            };
            Fill();
        }

        public ElementType? Result { get; private set; }

        /// <summary>Shows the list modally over AutoCAD; null when the person cancels.</summary>
        public static ElementType? Pick(string title, GeometryKind[] kinds)
        {
            var dlg = new TypePickerDialog(title, kinds);
            bool? ok = AcApp.ShowModalWindow(dlg);
            if (ok == true && dlg.Result != null)
            {
                Preferences.Current.Used(dlg.Result.Id);
                return dlg.Result;
            }

            return null;
        }

        private void Accept()
        {
            if (list.SelectedItem is ListBoxItem item && item.Tag is string id)
            {
                Result = AcadEnv.Catalog.FindType(id);
                DialogResult = Result != null;
            }
            else if (list.Items.Count == 1 && list.Items[0] is ListBoxItem only && only.Tag is string onlyId)
            {
                Result = AcadEnv.Catalog.FindType(onlyId);
                DialogResult = Result != null;
            }
        }

        private void Fill()
        {
            string q = search.Text.Trim();
            string cat = (category.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
            Preferences prefs = Preferences.Current;
            IEnumerable<ElementType> types = AcadEnv.Catalog.PlaceableTypes.Where(t => kinds.Length == 0 || kinds.Contains(t.Geometry));
            if (q.Length > 0)
            {
                // a search always looks through everything, whatever category is shown
                types = types.Where(t => (t.Name + " " + t.NameEn + " " + t.Id + " " + t.Subcategory).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            else if (cat == "*recent")
            {
                var pool = types.ToDictionary(t => t.Id);
                types = prefs.Recent.Where(pool.ContainsKey).Select(id => pool[id]);
            }
            else if (cat == "*fav")
            {
                types = types.Where(t => prefs.Favorites.Contains(t.Id));
            }
            else if (cat.Length > 0)
            {
                types = types.Where(t => t.Category == cat);
            }

            list.Items.Clear();
            foreach (ElementType t in types.Take(400))
            {
                if (!previews.TryGetValue(t.Id, out ImageSource? img))
                {
                    img = SitePalette.Preview(t);
                    previews[t.Id] = img;
                }

                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
                row.Children.Add(new Border
                {
                    Background = SitePalette.Back,
                    Width = 34,
                    Height = 34,
                    Margin = new Thickness(0, 0, 8, 0),
                    Child = new System.Windows.Controls.Image { Source = img, Stretch = Stretch.Uniform, Margin = new Thickness(3) },
                });
                var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                text.Children.Add(new TextBlock { Text = t.Name, FontSize = 13 });
                text.Children.Add(new TextBlock { Text = t.Subcategory + " · " + KindName(t.Geometry), FontSize = 11, Foreground = Brushes.Gray });
                row.Children.Add(text);
                list.Items.Add(new ListBoxItem { Content = row, Tag = t.Id, ToolTip = t.Builder.Description });
            }

            if (list.Items.Count > 0)
            {
                list.SelectedIndex = 0;
            }
        }

        private static string KindName(GeometryKind k) => k switch
        {
            GeometryKind.Point => "postavlja se klikom",
            GeometryKind.Path => "crta se kao linija (osovina)",
            _ => "crta se kao površina",
        };
    }
}
