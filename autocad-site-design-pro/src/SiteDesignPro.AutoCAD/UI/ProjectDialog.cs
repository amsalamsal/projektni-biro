using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Text;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.UI
{
    /// <summary>Answers of the project window.</summary>
    internal sealed class ProjectAnswers
    {
        public string Name { get; set; } = string.Empty;

        public string Number { get; set; } = string.Empty;

        public string Client { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        /// <summary>"mm", "cm" or "m".</summary>
        public string Unit { get; set; } = "mm";

        public double Scale { get; set; } = 500;

        public double NorthDegrees { get; set; }

        /// <summary>Absolute elevation of the building zero ±0,00 (m n.v.); null when not known yet.</summary>
        public double? BuildingZero { get; set; }

        /// <summary>True: the person picks the project origin in the drawing after the window closes.</summary>
        public bool PickOrigin { get; set; }
    }

    /// <summary>
    /// One window instead of eight command-line questions for a new project. Units default to the drawing's
    /// INSUNITS; everything except the name can be left as it is.
    /// </summary>
    internal sealed class ProjectDialog : Window
    {
        private readonly TextBox name = Field();
        private readonly TextBox number = Field();
        private readonly TextBox client = Field();
        private readonly TextBox location = Field();
        private readonly ComboBox unit = new ComboBox { Margin = new Thickness(0, 2, 0, 2) };
        private readonly ComboBox scale = new ComboBox { IsEditable = true, Margin = new Thickness(0, 2, 0, 2) };
        private readonly TextBox north = Field();
        private readonly TextBox zero = Field();
        private readonly CheckBox pickOrigin = new CheckBox { Content = "Ishodište ću odabrati u crtežu (inače 0,0,0)", Margin = new Thickness(0, 6, 0, 0) };
        private readonly TextBlock error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

        private ProjectDialog(ProjectSettings s, string currentUnit)
        {
            Title = "Novi projekat – Vanjsko uređenje PRO";
            Width = 470;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            name.Text = s.Name;
            number.Text = s.Number;
            client.Text = s.Client;
            location.Text = s.Location;
            foreach ((string code, string text) in new[] { ("mm", "Milimetri (uobičajeno)"), ("cm", "Centimetri"), ("m", "Metri") })
            {
                unit.Items.Add(new ComboBoxItem { Content = text, Tag = code });
                if (code == currentUnit)
                {
                    unit.SelectedIndex = unit.Items.Count - 1;
                }
            }

            if (unit.SelectedIndex < 0)
            {
                unit.SelectedIndex = 0;
            }

            foreach (string v in new[] { "100", "200", "250", "500", "1000", "2000" })
            {
                scale.Items.Add(v);
            }

            scale.Text = s.Labels.ScaleDenominator.ToString("0", CultureInfo.InvariantCulture);
            north.Text = Bs.Num(s.TrueNorthDegrees, 2);
            zero.Text = s.Elevations.BuildingZeroAbsolute.HasValue ? Bs.Num(s.Elevations.BuildingZeroAbsolute.Value, 2) : string.Empty;

            var grid = new Grid { Margin = new Thickness(14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int row = 0;
            void Add(string label, FrameworkElement editor, string? hint = null)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 8, 2) };
                Grid.SetRow(l, row);
                grid.Children.Add(l);
                Grid.SetRow(editor, row);
                Grid.SetColumn(editor, 1);
                grid.Children.Add(editor);
                editor.ToolTip = hint;
                row++;
            }

            Add("Naziv projekta *", name);
            Add("Broj projekta", number);
            Add("Investitor", client);
            Add("Lokacija", location);
            Add("Jedinica crteža", unit, "U kojim jedinicama crtate: 1 jedinica crteža = 1 mm, 1 cm ili 1 m.");
            Add("Mjerilo plana 1:", scale, "Određuje veličinu teksta oznaka i okvira lista.");
            Add("Pravi sjever (°)", north, "0 = sjever je gore u crtežu; pozitivno u smjeru kazaljke.");
            Add("Nulta kota ±0,00 (m n.v.)", zero, "Apsolutna visina poda prizemlja. Može ostati prazno i unijeti se kasnije (Nulta kota).");

            var panel = new StackPanel();
            panel.Children.Add(grid);
            var foot = new StackPanel { Margin = new Thickness(14, 0, 14, 14) };
            foot.Children.Add(pickOrigin);
            foot.Children.Add(new TextBlock
            {
                Text = "Brojeve možete pisati sa zarezom ili tačkom (npr. 520,15). Sve se kasnije može promijeniti (Postavke).",
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
            });
            foot.Children.Add(error);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var ok = new Button { Content = "U redu", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(8, 3, 8, 3) };
            var cancel = new Button { Content = "Odustani", IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 3, 8, 3) };
            ok.Click += (s2, e) => Accept();
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            foot.Children.Add(buttons);
            panel.Children.Add(foot);
            Content = panel;
            Loaded += (s2, e) => name.Focus();
        }

        public ProjectAnswers? Result { get; private set; }

        public static ProjectAnswers? Ask(ProjectSettings settings, string currentUnit)
        {
            var dlg = new ProjectDialog(settings, currentUnit);
            return AcApp.ShowModalWindow(dlg) == true ? dlg.Result : null;
        }

        private static TextBox Field() => new TextBox { Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(3) };

        private void Accept()
        {
            if (name.Text.Trim().Length == 0)
            {
                error.Text = "Upišite naziv projekta.";
                name.Focus();
                return;
            }

            if (!Bs.TryParse(scale.Text, out double sc) || sc <= 0)
            {
                error.Text = "Mjerilo mora biti broj veći od nule, npr. 500.";
                scale.Focus();
                return;
            }

            double n = 0;
            if (north.Text.Trim().Length > 0 && !Bs.TryParse(north.Text, out n))
            {
                error.Text = "Pravi sjever mora biti broj (stepeni).";
                north.Focus();
                return;
            }

            double? z = null;
            if (zero.Text.Trim().Length > 0)
            {
                if (!Bs.TryParse(zero.Text, out double zv))
                {
                    error.Text = "Nulta kota mora biti broj, npr. 520,15 – ili ostavite prazno.";
                    zero.Focus();
                    return;
                }

                z = zv;
            }

            Result = new ProjectAnswers
            {
                Name = name.Text.Trim(),
                Number = number.Text.Trim(),
                Client = client.Text.Trim(),
                Location = location.Text.Trim(),
                Unit = (unit.SelectedItem as ComboBoxItem)?.Tag as string ?? "mm",
                Scale = sc,
                NorthDegrees = n,
                BuildingZero = z,
                PickOrigin = pickOrigin.IsChecked == true,
            };
            DialogResult = true;
        }
    }
}
