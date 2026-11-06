using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SiteDesignPro.Core.Text;
using SiteDesignPro.Core.Urban;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.UI
{
    /// <summary>Window for the limits of the regulation plan; empty fields mean "no requirement".</summary>
    internal sealed class UrbanDialog : Window
    {
        private readonly TextBox coverage = Field();
        private readonly TextBox far = Field();
        private readonly TextBox green = Field();
        private readonly TextBox permeable = Field();
        private readonly TextBox dwellings = Field();
        private readonly TextBox perDwelling = Field();
        private readonly TextBox minParking = Field();
        private readonly TextBox accessible = Field();
        private readonly TextBox ev = Field();
        private readonly TextBlock error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

        private UrbanDialog(UrbanRequirements r)
        {
            Title = "Zahtjevi regulacionog plana – Vanjsko uređenje PRO";
            Width = 480;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            Set(coverage, r.MaxCoverage, 2);
            Set(far, r.MaxFloorAreaRatio, 2);
            Set(green, r.MinGreenPercent, 1);
            Set(permeable, r.MinPermeablePercent, 1);
            dwellings.Text = r.Dwellings?.ToString() ?? string.Empty;
            Set(perDwelling, r.ParkingPerDwelling, 2);
            minParking.Text = r.MinParking?.ToString() ?? string.Empty;
            Set(accessible, r.MinAccessiblePercent, 1);
            ev.Text = r.MinEvSpaces?.ToString() ?? string.Empty;

            var grid = new Grid { Margin = new Thickness(14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int row = 0;
            void Add(string label, FrameworkElement editor, string hint)
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

            Add("Koeficijent izgrađenosti – najviše", coverage, "Tlocrtna površina objekata / površina parcele, npr. 0,40.");
            Add("Koeficijent iskorištenosti – najviše", far, "Bruto građevinska površina / površina parcele, npr. 1,60.");
            Add("Zelene površine – najmanje (%)", green, "Npr. 30.");
            Add("Vodopropusne površine – najmanje (%)", permeable, "Zelenilo, travne rešetke, šljunak, kišni vrtovi. Npr. 40.");
            Add("Broj stanova", dwellings, "Za normativ parkinga po stanu.");
            Add("Parking mjesta po stanu", perDwelling, "Npr. 1,1.");
            Add("Parking mjesta – najmanje (ukupno)", minParking, "Ako plan zadaje broj mjesta umjesto norme po stanu.");
            Add("Mjesta za osobe s invaliditetom – najmanje (%)", accessible, "Npr. 5.");
            Add("Mjesta s punjačem za el. vozila – najmanje", ev, "Broj mjesta.");

            var panel = new StackPanel();
            panel.Children.Add(grid);
            var foot = new StackPanel { Margin = new Thickness(14, 0, 14, 14) };
            foot.Children.Add(new TextBlock
            {
                Text = "Prazno polje = nema zahtjeva (vrijednost se samo prikaže). Brojevi sa zarezom ili tačkom.",
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
            });
            foot.Children.Add(error);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var ok = new Button { Content = "U redu", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(8, 3, 8, 3) };
            var cancel = new Button { Content = "Odustani", IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 3, 8, 3) };
            ok.Click += (s, e) => Accept();
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            foot.Children.Add(buttons);
            panel.Children.Add(foot);
            Content = panel;
            Loaded += (s, e) => coverage.Focus();
        }

        public UrbanRequirements? Result { get; private set; }

        public static UrbanRequirements? Ask(UrbanRequirements current)
        {
            var dlg = new UrbanDialog(current);
            return AcApp.ShowModalWindow(dlg) == true ? dlg.Result : null;
        }

        private static TextBox Field() => new TextBox { Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(3) };

        private static void Set(TextBox box, double? v, int decimals) => box.Text = v.HasValue ? Bs.Num(v.Value, decimals) : string.Empty;

        private bool Read(TextBox box, string what, out double? value)
        {
            value = null;
            string t = box.Text.Trim();
            if (t.Length == 0)
            {
                return true;
            }

            if (Bs.TryParse(t, out double v) && v >= 0)
            {
                value = v;
                return true;
            }

            error.Text = what + " mora biti broj (ili prazno).";
            box.Focus();
            return false;
        }

        private void Accept()
        {
            if (!Read(coverage, "Koeficijent izgrađenosti", out double? kiz) || !Read(far, "Koeficijent iskorištenosti", out double? kis) ||
                !Read(green, "Zelene površine", out double? g) || !Read(permeable, "Vodopropusne površine", out double? p) ||
                !Read(dwellings, "Broj stanova", out double? d) || !Read(perDwelling, "Parking po stanu", out double? pd) ||
                !Read(minParking, "Parking mjesta", out double? mp) || !Read(accessible, "Mjesta za osobe s invaliditetom", out double? a) ||
                !Read(ev, "Mjesta s punjačem", out double? e))
            {
                return;
            }

            Result = new UrbanRequirements
            {
                MaxCoverage = kiz,
                MaxFloorAreaRatio = kis,
                MinGreenPercent = g,
                MinPermeablePercent = p,
                Dwellings = d.HasValue ? (int)System.Math.Round(d.Value) : (int?)null,
                ParkingPerDwelling = pd,
                MinParking = mp.HasValue ? (int)System.Math.Round(mp.Value) : (int?)null,
                MinAccessiblePercent = a,
                MinEvSpaces = e.HasValue ? (int)System.Math.Round(e.Value) : (int?)null,
            };
            DialogResult = true;
        }
    }
}
