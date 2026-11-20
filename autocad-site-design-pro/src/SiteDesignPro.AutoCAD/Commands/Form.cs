using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>
    /// The questions of a command in one place: a person answers them all in one window (<see cref="UI.FormDialog"/>),
    /// a script answers them one by one in the command line in the same order. Fields can depend on others
    /// (<see cref="FormField.When"/>: shown greyed out in the window, not asked in the command line) and a form with
    /// an id remembers the last answers as the next defaults.
    /// </summary>
    internal sealed class Form
    {
        public Form(string title, string? id = null, string? intro = null)
        {
            Title = title;
            Id = id;
            Intro = intro;
        }

        public string Title { get; }

        /// <summary>Key under which the answers are remembered (null: always the defaults of the code).</summary>
        public string? Id { get; }

        /// <summary>Text above the fields (what the command does, what follows in the drawing).</summary>
        public string? Intro { get; set; }

        /// <summary>Text below the fields, recomputed on every change (e.g. the resulting length or slope).</summary>
        public Func<FormValues, string>? Summary { get; set; }

        /// <summary>A check of the answers together: an error text keeps the window open (null: fine).</summary>
        public Func<FormValues, string?>? Check { get; set; }

        public List<FormField> Fields { get; } = new List<FormField>();

        public Form Header(string text)
        {
            Fields.Add(new FormField(FieldKind.Header, string.Empty, text));
            return this;
        }

        public Form Number(string key, string label, double value, double? min = null, double? max = null, string? hint = null, Func<FormValues, bool>? when = null, bool allowNegative = false, bool allowZero = false)
        {
            Fields.Add(new FormField(FieldKind.Number, key, label) { Value = value, Min = min ?? (allowNegative ? (double?)null : 0), Max = max, Hint = hint, When = when, AllowZero = allowZero || (min.HasValue && min.Value < 0) });
            return this;
        }

        public Form Integer(string key, string label, int value, int min = 1, int? max = null, string? hint = null, Func<FormValues, bool>? when = null)
        {
            Fields.Add(new FormField(FieldKind.Integer, key, label) { Value = value, Min = min, Max = max, Hint = hint, When = when });
            return this;
        }

        /// <summary>A choice; options as "Global|Shown" (the global name is returned) or plain names.</summary>
        public Form Choice(string key, string label, string[] options, string value, string? hint = null, Func<FormValues, bool>? when = null)
        {
            Fields.Add(new FormField(FieldKind.Choice, key, label) { Value = value, Options = options, Hint = hint, When = when });
            return this;
        }

        public Form Bool(string key, string label, bool value, string? hint = null, Func<FormValues, bool>? when = null)
        {
            Fields.Add(new FormField(FieldKind.Bool, key, label) { Value = value, Hint = hint, When = when });
            return this;
        }

        public Form Text(string key, string label, string value, string? hint = null, Func<FormValues, bool>? when = null, bool allowEmpty = true)
        {
            Fields.Add(new FormField(FieldKind.Text, key, label) { Value = value, Hint = hint, When = when, AllowEmpty = allowEmpty });
            return this;
        }

        public FormField Field(string key) => Fields.First(f => f.Key == key);

        /// <summary>The current values of all fields.</summary>
        public FormValues Values() => new FormValues(Fields.Where(f => f.Kind != FieldKind.Header).ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal));

        /// <summary>Takes the remembered answers of an earlier use as the starting values.</summary>
        public void Recall()
        {
            if (Id == null)
            {
                return;
            }

            UI.Preferences prefs = UI.Preferences.Current;
            foreach (FormField f in Fields.Where(x => x.Kind != FieldKind.Header && x.Remember))
            {
                string raw = prefs.Default("form:" + Id + ":" + f.Key, string.Empty);
                if (raw.Length > 0 && f.TryParse(raw, out object? v, out _))
                {
                    f.Value = v!;
                }
            }
        }

        public void Remember()
        {
            if (Id == null)
            {
                return;
            }

            UI.Preferences prefs = UI.Preferences.Current;
            prefs.Defaults ??= new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (FormField f in Fields.Where(x => x.Kind != FieldKind.Header && x.Remember))
            {
                prefs.Defaults["form:" + Id + ":" + f.Key] = f.Format();
            }

            prefs.Save();
        }
    }

    internal enum FieldKind
    {
        Header,
        Number,
        Integer,
        Choice,
        Bool,
        Text,
    }

    internal sealed class FormField
    {
        public FormField(FieldKind kind, string key, string label)
        {
            Kind = kind;
            Key = key;
            Label = label;
        }

        public FieldKind Kind { get; }

        public string Key { get; }

        public string Label { get; }

        public object Value { get; set; } = string.Empty;

        public double? Min { get; set; }

        public double? Max { get; set; }

        public bool AllowZero { get; set; } = true;

        public bool AllowEmpty { get; set; } = true;

        public string[] Options { get; set; } = Array.Empty<string>();

        public string? Hint { get; set; }

        public Func<FormValues, bool>? When { get; set; }

        /// <summary>False: never remembered (e.g. a value that depends on the drawing, such as a height read from it).</summary>
        public bool Remember { get; set; } = true;

        public static string Global(string option) => option.Split('|')[0];

        public static string Shown(string option)
        {
            string[] p = option.Split('|');
            return p[p.Length - 1];
        }

        public string Format() => Kind switch
        {
            FieldKind.Number => CommandContext.F((double)Value, 4),
            FieldKind.Integer => ((int)Value).ToString(CultureInfo.InvariantCulture),
            FieldKind.Bool => (bool)Value ? "1" : "0",
            _ => Value?.ToString() ?? string.Empty,
        };

        /// <summary>Reads a typed answer; the error says what is expected.</summary>
        public bool TryParse(string text, out object? value, out string error)
        {
            value = null;
            error = string.Empty;
            text = text.Trim();
            string name = Label.TrimEnd(':');
            switch (Kind)
            {
                case FieldKind.Number:
                    if (!Core.Text.Bs.TryParse(text, out double v))
                    {
                        error = name + ": upišite broj, npr. 2,5.";
                        return false;
                    }

                    if ((Min.HasValue && v < Min.Value - 1e-12) || (Max.HasValue && v > Max.Value + 1e-12))
                    {
                        error = name + ": vrijednost mora biti " + Range() + ".";
                        return false;
                    }

                    if (Math.Abs(v) < 1e-12 && !AllowZero)
                    {
                        error = name + ": vrijednost mora biti veća od nule.";
                        return false;
                    }

                    value = v;
                    return true;
                case FieldKind.Integer:
                    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ||
                        (Min.HasValue && i < Min.Value) || (Max.HasValue && i > Max.Value))
                    {
                        error = name + ": upišite cijeli broj " + Range() + ".";
                        return false;
                    }

                    value = i;
                    return true;
                case FieldKind.Bool:
                    value = text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("da", StringComparison.OrdinalIgnoreCase);
                    return true;
                case FieldKind.Choice:
                    string? match = Options.Select(Global).FirstOrDefault(o => o.Equals(text, StringComparison.OrdinalIgnoreCase));
                    if (match == null)
                    {
                        error = name + ": izaberite jednu od ponuđenih vrijednosti.";
                        return false;
                    }

                    value = match;
                    return true;
                default:
                    if (text.Length == 0 && !AllowEmpty)
                    {
                        error = name + ": upišite vrijednost.";
                        return false;
                    }

                    value = text;
                    return true;
            }
        }

        private string Range()
        {
            string Show(double x) => Kind == FieldKind.Integer ? ((int)x).ToString(CultureInfo.InvariantCulture) : CommandContext.F(x, 3);
            if (Min.HasValue && Max.HasValue)
            {
                return "od " + Show(Min.Value) + " do " + Show(Max.Value);
            }

            return Min.HasValue ? "najmanje " + Show(Min.Value) : Max.HasValue ? "najviše " + Show(Max.Value) : string.Empty;
        }
    }

    /// <summary>The answers of a form, by key.</summary>
    internal sealed class FormValues
    {
        private readonly Dictionary<string, object> values;

        public FormValues(Dictionary<string, object> values)
        {
            this.values = values;
        }

        public double Number(string key) => values[key] is double d ? d : Convert.ToDouble(values[key], CultureInfo.InvariantCulture);

        public int Integer(string key) => values[key] is int i ? i : Convert.ToInt32(values[key], CultureInfo.InvariantCulture);

        public string Choice(string key) => values[key]?.ToString() ?? string.Empty;

        public bool Bool(string key) => values[key] is bool b && b;

        public string Text(string key) => values[key]?.ToString() ?? string.Empty;

        public bool Is(string key, string option) => string.Equals(Choice(key), option, StringComparison.Ordinal);
    }
}
