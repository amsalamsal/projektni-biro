using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Input;

namespace SiteDesignPro.AutoCAD.UI
{
    /// <summary>
    /// Assigns the drawing's own layers to project roles. Layer names are never changed; the mapping is stored with
    /// the drawing and can be saved as a reusable preset in %APPDATA%\SiteDesignPro\mappings.
    /// </summary>
    internal sealed class LayerMappingDialog : Form
    {
        private const string NoRole = "— bez uloge —";
        private readonly DataGridView grid = new DataGridView();
        private readonly CheckBox markers = new CheckBox();
        private readonly Label hint = new Label();
        private readonly SortedDictionary<string, int> usage;

        public LayerMappingDialog(SortedDictionary<string, int> layerUsage, LayerMapping? current)
        {
            usage = layerUsage;
            Text = "Vanjsko uređenje PRO – mapiranje slojeva";
            Size = new Size(760, 620);
            MinimumSize = new Size(600, 420);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);

            var intro = new Label
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(8, 8, 8, 0),
                Text = "Dodijelite slojeve crteža ulogama projekta. Nazivi slojeva se ne mijenjaju. " +
                       "Slojevi bez uloge se ne koriste. Obavezne uloge: parcelna granica, tlocrt objekta i oznake kota.",
            };

            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "layer", HeaderText = "Sloj", ReadOnly = true, FillWeight = 45 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "count", HeaderText = "Objekata", ReadOnly = true, FillWeight = 12 });
            var roleColumn = new DataGridViewComboBoxColumn { Name = "role", HeaderText = "Uloga u projektu", FillWeight = 43, FlatStyle = FlatStyle.Flat };
            roleColumn.Items.Add(NoRole);
            foreach (SourceRole r in SourceRoles.All)
            {
                roleColumn.Items.Add(SourceRoles.Name(r));
            }

            grid.Columns.Add(roleColumn);
            grid.CellEnter += (s, e) => ShowHint(e.RowIndex);
            grid.CurrentCellDirtyStateChanged += (s, e) => { if (grid.IsCurrentCellDirty) { grid.CommitEdit(DataGridViewDataErrorContexts.Commit); } };
            grid.DataError += (s, e) => e.ThrowException = false;

            hint.Dock = DockStyle.Bottom;
            hint.Height = 40;
            hint.Padding = new Padding(8, 4, 8, 0);
            hint.ForeColor = SystemColors.GrayText;

            markers.Text = "Dozvoli rad s oznakama objekata (blok umjesto tlocrta; dimenzije se potvrđuju ručno)";
            markers.AutoSize = true;
            markers.Dock = DockStyle.Bottom;
            markers.Padding = new Padding(8, 0, 0, 0);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var ok = new Button { Text = "U redu", DialogResult = DialogResult.OK, Width = 90 };
            var cancel = new Button { Text = "Otkaži", DialogResult = DialogResult.Cancel, Width = 90 };
            var save = new Button { Text = "Sačuvaj predložak...", Width = 140 };
            var load = new Button { Text = "Učitaj predložak...", Width = 140 };
            var suggest = new Button { Text = "Prijedlog prema nazivima", Width = 170 };
            save.Click += (s, e) => SavePreset();
            load.Click += (s, e) => LoadPreset();
            suggest.Click += (s, e) => Apply(LayerMapping.Suggest(usage.Keys), onlyEmpty: true);
            buttons.Controls.AddRange(new Control[] { ok, cancel, save, load, suggest });
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.Add(grid);
            Controls.Add(intro);
            Controls.Add(hint);
            Controls.Add(markers);
            Controls.Add(buttons);

            foreach (KeyValuePair<string, int> kv in usage)
            {
                grid.Rows.Add(kv.Key, kv.Value, NoRole);
            }

            if (current != null)
            {
                Apply(current, onlyEmpty: false);
                markers.Checked = current.AllowMarkerWorkflow;
                MappingName = current.Name;
            }
        }

        public string MappingName { get; private set; } = "Mapiranje crteža";

        public static string PresetFolder => Path.Combine(AcadEnv.UserDirectory, "mappings");

        /// <summary>Mapping as edited in the dialog (exact layer names).</summary>
        public LayerMapping Result()
        {
            var m = new LayerMapping { Name = MappingName, AllowMarkerWorkflow = markers.Checked };
            foreach (SourceRole role in SourceRoles.All)
            {
                string name = SourceRoles.Name(role);
                List<string> layers = grid.Rows.Cast<DataGridViewRow>().Where(r => (r.Cells["role"].Value as string) == name).Select(r => (string)r.Cells["layer"].Value).ToList();
                if (layers.Count > 0)
                {
                    m.Set(role, layers);
                }
            }

            return m;
        }

        private void Apply(LayerMapping mapping, bool onlyEmpty)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (onlyEmpty && (row.Cells["role"].Value as string) != NoRole)
                {
                    continue;
                }

                List<SourceRole> roles = mapping.RolesOf((string)row.Cells["layer"].Value);
                row.Cells["role"].Value = roles.Count > 0 ? SourceRoles.Name(roles[0]) : (onlyEmpty ? NoRole : NoRole);
            }
        }

        private void ShowHint(int row)
        {
            if (row < 0 || row >= grid.Rows.Count)
            {
                return;
            }

            string? roleName = grid.Rows[row].Cells["role"].Value as string;
            SourceRole? role = SourceRoles.All.Cast<SourceRole?>().FirstOrDefault(r => SourceRoles.Name(r!.Value) == roleName);
            hint.Text = role.HasValue ? SourceRoles.Hint(role.Value) : "Odaberite ulogu ovog sloja u projektu.";
        }

        private void SavePreset()
        {
            using (var d = new SaveFileDialog { Title = "Sačuvaj predložak mapiranja", InitialDirectory = EnsureFolder(), Filter = "Predložak mapiranja (*.sdpmap.json)|*.sdpmap.json", FileName = MappingName + ".sdpmap.json" })
            {
                if (d.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                MappingName = Path.GetFileName(d.FileName).Replace(".sdpmap.json", string.Empty);
                LayerMapping m = Result();
                Core.Safety.SafeFile.WriteAllTextAtomic(d.FileName, m.ToJson());
            }
        }

        private void LoadPreset()
        {
            using (var d = new OpenFileDialog { Title = "Učitaj predložak mapiranja", InitialDirectory = EnsureFolder(), Filter = "Predložak mapiranja (*.sdpmap.json)|*.sdpmap.json" })
            {
                if (d.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    LayerMapping m = LayerMapping.FromJson(File.ReadAllText(d.FileName));
                    Apply(m, onlyEmpty: false);
                    markers.Checked = m.AllowMarkerWorkflow;
                    MappingName = m.Name;
                }
                catch (Exception ex) when (ex is IOException || ex is FormatException || ex is System.Text.Json.JsonException)
                {
                    MessageBox.Show(this, "Predložak nije moguće učitati: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private static string EnsureFolder()
        {
            Directory.CreateDirectory(PresetFolder);
            return PresetFolder;
        }
    }
}
