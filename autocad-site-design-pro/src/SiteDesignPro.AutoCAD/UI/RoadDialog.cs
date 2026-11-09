using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SiteDesignPro.Core.Roads;
using SiteDesignPro.Core.Text;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.UI
{
    /// <summary>Cross-section of a road: direction, lanes, lane width, sidewalks, bike lanes, kerb radius and arrows.</summary>
    internal sealed class RoadDialog : Window
    {
        /// <summary>Usual cross-sections; picking one fills the fields (they can still be changed).</summary>
        private static readonly (string Name, RoadSpec Spec)[] Profiles =
        {
            ("Stambena ulica (1 + 1 × 2,75 m, trotoari 1,60 m)", new RoadSpec { LanesLeft = 1, LanesRight = 1, LaneWidth = 2.75, SidewalkLeft = 1.6, SidewalkRight = 1.6, CornerRadius = 6 }),
            ("Sabirna ulica (1 + 1 × 3,25 m, bic. staze 1,50 m, trotoari 2,00 m)", new RoadSpec { LanesLeft = 1, LanesRight = 1, LaneWidth = 3.25, BikeLeft = 1.5, BikeRight = 1.5, SidewalkLeft = 2.0, SidewalkRight = 2.0, CornerRadius = 6 }),
            ("Bulevar (2 + 2 × 3,25 m, bic. staze 1,50 m, trotoari 2,50 m)", new RoadSpec { LanesLeft = 2, LanesRight = 2, LaneWidth = 3.25, BikeLeft = 1.5, BikeRight = 1.5, SidewalkLeft = 2.5, SidewalkRight = 2.5, CornerRadius = 8 }),
            ("Jednosmjerna ulica (1 × 3,50 m, trotoari 1,60 m)", new RoadSpec { OneWay = true, LanesRight = 1, LaneWidth = 3.5, SidewalkLeft = 1.6, SidewalkRight = 1.6, CornerRadius = 6 }),
            ("Kolni prilaz (1 × 3,50 m, bez trotoara)", new RoadSpec { OneWay = true, LanesRight = 1, LaneWidth = 3.5, SidewalkLeft = 0, SidewalkRight = 0, CornerRadius = 5, Arrows = false }),
        };

        private readonly ComboBox profile = new ComboBox { Margin = new Thickness(0, 2, 0, 2) };
        private readonly RadioButton twoWay = new RadioButton { Content = "Dvosmjerna", Margin = new Thickness(0, 0, 16, 0) };
        private readonly RadioButton oneWay = new RadioButton { Content = "Jednosmjerna" };
        private readonly ComboBox lanesLeft = Lanes();
        private readonly ComboBox lanesRight = Lanes();
        private readonly TextBlock lanesLeftLabel = new TextBlock();
        private readonly TextBlock lanesRightLabel = new TextBlock();
        private readonly TextBox laneWidth = Field();
        private readonly TextBox walkLeft = Field();
        private readonly TextBox walkRight = Field();
        private readonly TextBox bikeLeft = Field();
        private readonly TextBox bikeRight = Field();
        private readonly TextBox radius = Field();
        private readonly TextBlock effective = new TextBlock { Foreground = Brushes.DimGray, Margin = new Thickness(0, 2, 0, 6), TextWrapping = TextWrapping.Wrap };
        private readonly CheckBox arrows = new CheckBox { Content = "Strelice smjera kretanja na kolovozu, svakih", VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBox arrowSpacing = new TextBox { Width = 50, Margin = new Thickness(6, 0, 4, 0), Padding = new Thickness(2) };
        private readonly TextBlock summary = new TextBlock { Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        private readonly TextBlock error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

        private RoadDialog(RoadSpec s, string title, string units)
        {
            Title = title + " – Vanjsko uređenje PRO";
            Width = 520;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            Fill(s);

            var grid = new Grid { Margin = new Thickness(14, 10, 14, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int row = 0;
            void Add(FrameworkElement label, FrameworkElement editor, string? hint = null)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                label.VerticalAlignment = VerticalAlignment.Center;
                label.Margin = new Thickness(0, 3, 8, 3);
                Grid.SetRow(label, row);
                grid.Children.Add(label);
                Grid.SetRow(editor, row);
                Grid.SetColumn(editor, 1);
                grid.Children.Add(editor);
                if (hint != null)
                {
                    editor.ToolTip = hint;
                }

                row++;
            }

            void Header(string text)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var h = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 2) };
                Grid.SetRow(h, row);
                Grid.SetColumnSpan(h, 2);
                grid.Children.Add(h);
                row++;
            }

            TextBlock L(string t) => new TextBlock { Text = t };

            var direction = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            direction.Children.Add(twoWay);
            direction.Children.Add(oneWay);
            profile.Items.Add("— zadnje korištene vrijednosti —");
            foreach ((string name, RoadSpec _) in Profiles)
            {
                profile.Items.Add(name);
            }

            profile.SelectedIndex = 0;
            profile.SelectionChanged += (o, e) =>
            {
                if (profile.SelectedIndex > 0)
                {
                    Fill(Profiles[profile.SelectedIndex - 1].Spec);
                }
            };
            Header("Saobraćajnica");
            Add(L("Profil"), profile, "Uobičajeni presjeci; vrijednosti se mogu dalje mijenjati.");
            Add(L("Smjer"), direction);
            Add(lanesLeftLabel, lanesLeft, "Trake lijevo od osovine voze suprotno od smjera crtanja osovine.");
            Add(lanesRightLabel, lanesRight, "Trake desno od osovine voze u smjeru crtanja osovine.");
            Add(L("Širina saobraćajne trake (m)"), laneWidth, "Npr. 3,00 za stambenu ulicu, 3,25–3,50 za sabirnu.");
            Header("Trotoari i biciklističke staze (0 = nema)");
            Add(L("Trotoar lijevo (m, 0–5)"), walkLeft, "Hatch tamno sivi, 30 % prozirnosti.");
            Add(L("Trotoar desno (m, 0–5)"), walkRight, "Hatch tamno sivi, 30 % prozirnosti.");
            Add(L("Biciklistička staza lijevo (m, 0–2)"), bikeLeft, "Uz kolovoz, prije trotoara. Hatch bordo, 30 % prozirnosti.");
            Add(L("Biciklistička staza desno (m, 0–2)"), bikeRight, "Uz kolovoz, prije trotoara. Hatch bordo, 30 % prozirnosti.");
            Header("Raskrsnice");
            Add(L("Radijus ivičnjaka – stvarni (m)"), radius,
                "Stambena zona do 40 km/h, putnička i manja teretna vozila: 5–8 m. Ivice kolovoza, staza i trotoara prate isti centar.");

            var arrowRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 4, 14, 0) };
            arrowRow.Children.Add(arrows);
            arrowRow.Children.Add(arrowSpacing);
            arrowRow.Children.Add(new TextBlock { Text = "m", VerticalAlignment = VerticalAlignment.Center });

            var panel = new StackPanel();
            panel.Children.Add(grid);
            var eff = new StackPanel { Margin = new Thickness(14, 0, 14, 0) };
            eff.Children.Add(effective);
            panel.Children.Add(eff);
            panel.Children.Add(arrowRow);
            var foot = new StackPanel { Margin = new Thickness(14, 6, 14, 14) };
            summary.Text = "Uz kolovoz se crta ivičnjak 15 cm, a uz vanjsku ivicu trotoara ivičnjak 10 cm. Isprekidane linije traka, " +
                           "spajanje s drugim ulicama i cestama i zaobljeni uglovi raskrsnica rade se automatski. Rasvjeta se dodaje posebno (Rasvjeta ili Duž linije). " +
                           "Jedinice crteža: " + units + ".";
            foot.Children.Add(summary);
            foot.Children.Add(error);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var ok = new Button { Content = "U redu", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(8, 3, 8, 3) };
            var cancel = new Button { Content = "Odustani", IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 3, 8, 3) };
            ok.Click += (o, e) => Accept();
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            foot.Children.Add(buttons);
            panel.Children.Add(foot);
            Content = panel;

            twoWay.Checked += (o, e) => Refresh();
            oneWay.Checked += (o, e) => Refresh();
            radius.TextChanged += (o, e) => Refresh();
            bikeLeft.TextChanged += (o, e) => Refresh();
            bikeRight.TextChanged += (o, e) => Refresh();
            arrows.Checked += (o, e) => Refresh();
            arrows.Unchecked += (o, e) => Refresh();
            Refresh();
            Loaded += (o, e) => laneWidth.Focus();
        }

        public RoadSpec? Result { get; private set; }

        private void Fill(RoadSpec s)
        {
            twoWay.IsChecked = !s.OneWay;
            oneWay.IsChecked = s.OneWay;
            lanesLeft.SelectedIndex = Math.Max(0, Math.Min(2, s.LanesLeft - 1));
            lanesRight.SelectedIndex = Math.Max(0, Math.Min(2, s.LanesRight - 1));
            laneWidth.Text = Bs.Num(s.LaneWidth, 2);
            walkLeft.Text = Bs.Num(s.SidewalkLeft, 2);
            walkRight.Text = Bs.Num(s.SidewalkRight, 2);
            bikeLeft.Text = Bs.Num(s.BikeLeft, 2);
            bikeRight.Text = Bs.Num(s.BikeRight, 2);
            radius.Text = Bs.Num(s.CornerRadius, 2);
            arrows.IsChecked = s.Arrows;
            arrowSpacing.Text = Bs.Num(s.ArrowSpacing, 0);
        }

        public static RoadSpec? Ask(RoadSpec current, string title, string units)
        {
            var dlg = new RoadDialog(current, title, units);
            return AcApp.ShowModalWindow(dlg) == true ? dlg.Result : null;
        }

        private static ComboBox Lanes()
        {
            var c = new ComboBox { Margin = new Thickness(0, 2, 0, 2), Width = 70, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (int i in Enumerable.Range(1, 3))
            {
                c.Items.Add(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            return c;
        }

        /// <summary>Number field whose text is selected on entry, so a new value simply replaces the old one.</summary>
        private static TextBox Field()
        {
            var box = new TextBox { Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(3) };
            box.GotKeyboardFocus += (s, e) => box.SelectAll();
            box.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (!box.IsKeyboardFocusWithin)
                {
                    e.Handled = true;
                    box.Focus();
                }
            };
            return box;
        }

        private void Refresh()
        {
            bool one = oneWay.IsChecked == true;
            lanesLeftLabel.Text = one ? "—" : "Broj traka lijevo (1–3)";
            lanesLeft.IsEnabled = !one;
            lanesRightLabel.Text = one ? "Broj traka (1–3)" : "Broj traka desno (1–3)";
            arrowSpacing.IsEnabled = arrows.IsChecked == true;
            double bike = 0;
            if (Bs.TryParse(bikeLeft.Text, out double bl))
            {
                bike = bl;
            }

            if (Bs.TryParse(bikeRight.Text, out double br) && br > bike)
            {
                bike = br;
            }

            effective.Text = Bs.TryParse(radius.Text, out double r)
                ? "Efektivni radijus (ivica kolovoza) = " + Bs.Num(r + bike, 2) + " m" + (bike > 0 ? " (stvarni + biciklistička staza)." : " (isti kao stvarni, nema biciklističke staze).") +
                  " Radijusi se vide kad kursor stoji na liniji ivičnjaka."
                : string.Empty;
        }

        private bool Read(TextBox box, string what, double min, double max, out double value)
        {
            value = 0;
            if (Bs.TryParse(box.Text.Trim(), out double v) && v >= min && v <= max)
            {
                value = v;
                return true;
            }

            error.Text = what + " mora biti broj od " + Bs.Num(min, 2) + " do " + Bs.Num(max, 2) + ".";
            box.Focus();
            return false;
        }

        private void Accept()
        {
            if (!Read(laneWidth, "Širina saobraćajne trake", 2.5, 4.5, out double lw) ||
                !Read(walkLeft, "Lijevi trotoar", 0, RoadSpec.MaxSidewalk, out double wl) ||
                !Read(walkRight, "Desni trotoar", 0, RoadSpec.MaxSidewalk, out double wr) ||
                !Read(bikeLeft, "Lijeva biciklistička staza", 0, RoadSpec.MaxBike, out double bl) ||
                !Read(bikeRight, "Desna biciklistička staza", 0, RoadSpec.MaxBike, out double br) ||
                !Read(radius, "Radijus ivičnjaka", 1, 30, out double r))
            {
                return;
            }

            double spacing = 40;
            if (arrows.IsChecked == true && !Read(arrowSpacing, "Razmak strelica", 10, 200, out spacing))
            {
                return;
            }

            Result = new RoadSpec
            {
                OneWay = oneWay.IsChecked == true,
                LanesLeft = lanesLeft.SelectedIndex + 1,
                LanesRight = lanesRight.SelectedIndex + 1,
                LaneWidth = lw,
                SidewalkLeft = wl,
                SidewalkRight = wr,
                BikeLeft = bl,
                BikeRight = br,
                CornerRadius = r,
                Arrows = arrows.IsChecked == true,
                ArrowSpacing = spacing,
            };
            DialogResult = true;
        }
    }
}
