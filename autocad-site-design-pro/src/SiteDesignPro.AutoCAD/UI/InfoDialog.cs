using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.UI
{
    /// <summary>
    /// Result of a command in a window instead of the command line: a heading, the counts as rows (label – value),
    /// notes and warnings, and buttons. The text can be copied.
    /// </summary>
    internal sealed class InfoDialog : Window
    {
        private string chosen = string.Empty;

        private InfoDialog(string title, string heading, IEnumerable<(string Label, string Value)> rows, IEnumerable<string> notes, IEnumerable<string> warnings, IReadOnlyList<(string Id, string Text)> buttons)
        {
            Title = title + " – Vanjsko uređenje PRO";
            Width = 520;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            var panel = new StackPanel { Margin = new Thickness(16, 12, 16, 14) };
            panel.Children.Add(new TextBlock { Text = heading, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int r = 0;
            foreach ((string label, string value) in rows)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var l = new TextBlock { Text = label, Margin = new Thickness(0, 2, 12, 2), TextWrapping = TextWrapping.Wrap };
                var v = new TextBlock { Text = value, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetRow(l, r);
                Grid.SetRow(v, r);
                Grid.SetColumn(v, 1);
                grid.Children.Add(l);
                grid.Children.Add(v);
                r++;
            }

            panel.Children.Add(grid);
            AddList(panel, warnings, Brushes.Firebrick);
            AddList(panel, notes, Brushes.DimGray);

            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            foreach ((string id, string text) in buttons)
            {
                var b = new Button { Content = text, MinWidth = 90, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(10, 3, 10, 3), IsDefault = id == buttons[0].Id, IsCancel = id == "close" };
                string captured = id;
                b.Click += (o, e) =>
                {
                    chosen = captured;
                    DialogResult = true;
                };
                bar.Children.Add(b);
            }

            panel.Children.Add(bar);
            Content = panel;
        }

        private static void AddList(StackPanel panel, IEnumerable<string> lines, Brush brush)
        {
            List<string> list = lines.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (list.Count == 0)
            {
                return;
            }

            var box = new TextBox
            {
                Text = string.Join("\n", list.Select(x => "• " + x)),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = brush,
                Margin = new Thickness(0, 8, 0, 0),
                MaxHeight = 220,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            panel.Children.Add(box);
        }

        /// <summary>Shows the result; returns the id of the button pressed ("close" when the window is closed).</summary>
        public static string Present(string title, string heading, IEnumerable<(string Label, string Value)> rows, IEnumerable<string>? notes = null, IEnumerable<string>? warnings = null, params (string Id, string Text)[] buttons)
        {
            if (buttons.Length == 0)
            {
                buttons = new[] { ("close", "Zatvori") };
            }

            var dlg = new InfoDialog(title, heading, rows, notes ?? Enumerable.Empty<string>(), warnings ?? Enumerable.Empty<string>(), buttons);
            return AcApp.ShowModalWindow(dlg) == true ? dlg.chosen : "close";
        }
    }
}
