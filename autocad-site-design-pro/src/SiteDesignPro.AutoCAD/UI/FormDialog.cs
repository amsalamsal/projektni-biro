using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SiteDesignPro.AutoCAD.Commands;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.UI
{
    /// <summary>
    /// The window of a <see cref="Form"/>: every question of a command at once, with the current values, hints on
    /// hover, fields that depend on others greyed out, a live summary and the errors in red. OK takes the answers.
    /// </summary>
    internal sealed class FormDialog : Window
    {
        private readonly Form form;
        private readonly Dictionary<string, Func<string>> readers = new Dictionary<string, Func<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Action<object>> writers = new Dictionary<string, Action<object>>(StringComparer.Ordinal);
        private readonly Dictionary<string, (FrameworkElement Label, FrameworkElement Editor)> rows = new Dictionary<string, (FrameworkElement, FrameworkElement)>(StringComparer.Ordinal);
        private readonly TextBlock summary = new TextBlock { Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        private readonly TextBlock error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
        private FrameworkElement? first;

        private FormDialog(Form form)
        {
            this.form = form;
            Title = form.Title + " – Vanjsko uređenje PRO";
            Width = 540;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            var panel = new StackPanel { Margin = new Thickness(14, 10, 14, 14) };
            if (!string.IsNullOrEmpty(form.Intro))
            {
                panel.Children.Add(new TextBlock { Text = form.Intro, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int row = 0;
            foreach (FormField f in form.Fields)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                if (f.Kind == FieldKind.Header)
                {
                    var h = new TextBlock { Text = f.Label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 2), TextWrapping = TextWrapping.Wrap };
                    Grid.SetRow(h, row);
                    Grid.SetColumnSpan(h, 2);
                    grid.Children.Add(h);
                    row++;
                    continue;
                }

                FrameworkElement editor = Editor(f);
                var label = new TextBlock { Text = f.Label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 8, 3) };
                if (f.Kind == FieldKind.Bool)
                {
                    label.Text = string.Empty;
                }

                if (f.Hint != null)
                {
                    label.ToolTip = f.Hint;
                    editor.ToolTip = f.Hint;
                }

                editor.Margin = new Thickness(0, 3, 0, 3);
                Grid.SetRow(label, row);
                Grid.SetRow(editor, row);
                Grid.SetColumn(editor, 1);
                if (f.Kind == FieldKind.Bool)
                {
                    Grid.SetColumn(editor, 0);
                    Grid.SetColumnSpan(editor, 2);
                }

                grid.Children.Add(label);
                grid.Children.Add(editor);
                rows[f.Key] = (label, editor);
                first ??= editor;
                row++;
            }

            // a long form (all parameters of an element) scrolls; the buttons stay visible
            panel.Children.Add(new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height * 0.62) });
            panel.Children.Add(summary);
            panel.Children.Add(error);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            if (form.Defaults.Count > 0)
            {
                // back to the values of the program when the remembered answers no longer fit
                var reset = new Button { Content = "Zadane vrijednosti", MinWidth = 90, Margin = new Thickness(0, 0, 18, 0), Padding = new Thickness(8, 3, 8, 3), IsEnabled = form.Recalled, ToolTip = "Vraća vrijednosti programa umjesto zapamćenih zadnjih unosa." };
                reset.Click += (o, e) =>
                {
                    foreach (KeyValuePair<string, object> kv in form.Defaults)
                    {
                        if (writers.TryGetValue(kv.Key, out Action<object>? write))
                        {
                            write(kv.Value);
                        }
                    }

                    Refresh();
                };
                buttons.Children.Add(reset);
            }

            var ok = new Button { Content = "U redu", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(8, 3, 8, 3) };
            var cancel = new Button { Content = "Odustani", IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 3, 8, 3) };
            ok.Click += (o, e) => Accept();
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            Content = panel;
            Refresh();
            Loaded += (o, e) => first?.Focus();
        }

        /// <summary>Shows the form; false when the person cancels. The answers are written into the fields.</summary>
        public static bool Ask(Form form)
        {
            var dlg = new FormDialog(form);
            return AcApp.ShowModalWindow(dlg) == true;
        }

        private FrameworkElement Editor(FormField f)
        {
            switch (f.Kind)
            {
                case FieldKind.Bool:
                    {
                        var box = new CheckBox { Content = f.Label, IsChecked = (bool)f.Value, VerticalAlignment = VerticalAlignment.Center };
                        box.Checked += (o, e) => Refresh();
                        box.Unchecked += (o, e) => Refresh();
                        readers[f.Key] = () => box.IsChecked == true ? "1" : "0";
                        writers[f.Key] = v => box.IsChecked = v is bool b && b;
                        return box;
                    }

                case FieldKind.Choice:
                    {
                        string current = (string)f.Value;
                        if (f.Options.Length <= 4 && f.Options.Sum(o => FormField.Shown(o).Length) <= 46)
                        {
                            var panel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
                            var buttons = new List<(RadioButton Button, string Value)>();
                            string group = "g" + f.Key;
                            foreach (string o in f.Options)
                            {
                                var rb = new RadioButton { Content = FormField.Shown(o), GroupName = group, Margin = new Thickness(0, 0, 14, 0), IsChecked = FormField.Global(o) == current };
                                rb.Checked += (s, e) => Refresh();
                                buttons.Add((rb, FormField.Global(o)));
                                panel.Children.Add(rb);
                            }

                            readers[f.Key] = () => buttons.FirstOrDefault(b => b.Button.IsChecked == true).Value ?? current;
                            writers[f.Key] = v => buttons.ForEach(b => b.Button.IsChecked = b.Value == (v as string));
                            return panel;
                        }

                        var combo = new ComboBox();
                        foreach (string o in f.Options)
                        {
                            combo.Items.Add(FormField.Shown(o));
                        }

                        combo.SelectedIndex = Math.Max(0, Array.FindIndex(f.Options, o => FormField.Global(o) == current));
                        combo.SelectionChanged += (s, e) => Refresh();
                        readers[f.Key] = () => FormField.Global(f.Options[Math.Max(0, combo.SelectedIndex)]);
                        writers[f.Key] = v => combo.SelectedIndex = Math.Max(0, Array.FindIndex(f.Options, o => FormField.Global(o) == (v as string)));
                        return combo;
                    }

                default:
                    {
                        var box = new TextBox { Padding = new Thickness(3), Text = f.Format() };
                        if (f.Kind == FieldKind.Number || f.Kind == FieldKind.Integer)
                        {
                            box.Width = 110;
                            box.HorizontalAlignment = HorizontalAlignment.Left;
                        }

                        box.GotKeyboardFocus += (s, e) => box.SelectAll();
                        box.PreviewMouseLeftButtonDown += (s, e) =>
                        {
                            if (!box.IsKeyboardFocusWithin)
                            {
                                e.Handled = true;
                                box.Focus();
                            }
                        };
                        box.TextChanged += (s, e) => Refresh();
                        readers[f.Key] = () => box.Text;
                        writers[f.Key] = v => box.Text = f.Format(v);
                        return box;
                    }
            }
        }

        /// <summary>Reads the fields as they are now (fields that do not parse keep their last value).</summary>
        private FormValues Current(List<string>? errors)
        {
            var map = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (FormField f in form.Fields.Where(x => x.Kind != FieldKind.Header))
            {
                map[f.Key] = f.Value;
            }

            var values = new FormValues(map);
            foreach (FormField f in form.Fields.Where(x => x.Kind != FieldKind.Header))
            {
                if (f.TryParse(readers[f.Key](), out object? v, out string err))
                {
                    map[f.Key] = v!;
                }
                else if (errors != null && (f.When == null || f.When(values)))
                {
                    errors.Add(err);
                }
            }

            return values;
        }

        private void Refresh()
        {
            if (readers.Count < form.Fields.Count(x => x.Kind != FieldKind.Header))
            {
                return; // still being built
            }

            FormValues values = Current(null);
            foreach (FormField f in form.Fields.Where(x => x.When != null))
            {
                bool on = f.When!(values);
                (FrameworkElement label, FrameworkElement editor) = rows[f.Key];
                label.IsEnabled = on;
                editor.IsEnabled = on;
                label.Opacity = on ? 1 : 0.45;
            }

            summary.Text = form.Summary?.Invoke(values) ?? string.Empty;
            summary.Visibility = summary.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            error.Text = string.Empty;
        }

        private void Accept()
        {
            var errors = new List<string>();
            FormValues values = Current(errors);
            string? check = errors.Count == 0 ? form.Check?.Invoke(values) : null;
            if (errors.Count > 0 || check != null)
            {
                error.Text = errors.Count > 0 ? string.Join("\n", errors) : check!;
                return;
            }

            foreach (FormField f in form.Fields.Where(x => x.Kind != FieldKind.Header))
            {
                f.TryParse(readers[f.Key](), out object? v, out _);
                if (v != null)
                {
                    f.Value = v;
                }
            }

            DialogResult = true;
        }
    }
}
