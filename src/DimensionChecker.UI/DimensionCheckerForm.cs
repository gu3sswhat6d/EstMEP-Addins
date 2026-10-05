using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using EstMepAddins.DimensionChecker.Models;
using EstMepAddins.DimensionChecker.Verification;

namespace EstMepAddins.DimensionChecker.UI
{
    /// <summary>
    /// Main window: pick an ITM and a submittal PDF, draw every size, have Claude check the
    /// dimensions, and show pass/fail with fix instructions.
    /// </summary>
    public sealed class DimensionCheckerForm : Form
    {
        private readonly IItemDrawingService _items;

        private readonly TextBox _itmPath = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        private readonly TextBox _pdfPath = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        private readonly ComboBox _units = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        private readonly TextBox _tolerance = new TextBox { Text = "1/16 in", Width = 80 };
        private readonly Button _run = new Button { Text = "Draw all sizes and verify", AutoSize = true, Enabled = false };
        private readonly Button _save = new Button { Text = "Save report…", AutoSize = true, Enabled = false };
        private readonly Button _apiKey = new Button { Text = "API key…", AutoSize = true };
        private readonly Label _status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };

        private readonly Label _headline = new Label
        {
            Dock = DockStyle.Top, Height = 36, TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 13f, FontStyle.Bold), Padding = new Padding(4, 0, 0, 0),
        };
        private readonly TextBox _summary = new TextBox { Dock = DockStyle.Top, Height = 60, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly DataGridView _checks = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };
        private readonly TextBox _fixes = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = true,
            Font = new Font(FontFamily.GenericMonospace, 9f),
        };

        private ItemSnapshot? _lastSnapshot;
        private VerificationReport? _lastReport;
        private CancellationTokenSource? _cancel;

        public DimensionCheckerForm(IItemDrawingService items)
        {
            _items = items;

            Text = "Submittal Dimension Check";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(980, 760);
            MinimumSize = new Size(720, 520);
            ShowIcon = false;

            _units.Items.AddRange(new object[] { "inches", "millimeters" });
            _units.SelectedIndex = 0;
            _units.SelectedIndexChanged += (s, e) => _tolerance.Text = (string)_units.SelectedItem! == "inches" ? "1/16 in" : "1 mm";

            _checks.Columns.Add("Size", "Size");
            _checks.Columns.Add("Dimension", "ITM dimension");
            _checks.Columns.Add("Label", "Submittal");
            _checks.Columns.Add("Itm", "ITM value");
            _checks.Columns.Add("Submittal", "Submittal value");
            _checks.Columns.Add("Status", "Result");
            _checks.Columns.Add("Note", "Note");
            _checks.Columns["Note"]!.FillWeight = 220;

            Controls.Add(BuildResultsArea());
            Controls.Add(BuildInputsArea());

            _run.Click += async (s, e) => await RunAsync();
            _save.Click += (s, e) => SaveReport();
            _apiKey.Click += (s, e) => PromptForApiKey();
            FormClosing += (s, e) => _cancel?.Cancel();
        }

        private Control BuildInputsArea()
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, AutoSize = true, Padding = new Padding(8) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var browseItm = new Button { Text = "Browse…", AutoSize = true };
            browseItm.Click += (s, e) => BrowseItm();
            var browsePdf = new Button { Text = "Browse…", AutoSize = true };
            browsePdf.Click += (s, e) => BrowsePdf();

            grid.Controls.Add(new Label { Text = "Item (ITM):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            grid.Controls.Add(_itmPath, 1, 0);
            grid.Controls.Add(browseItm, 2, 0);
            grid.Controls.Add(new Label { Text = "Submittal (PDF):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            grid.Controls.Add(_pdfPath, 1, 1);
            grid.Controls.Add(browsePdf, 2, 1);

            var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            options.Controls.Add(new Label { Text = "ITM units:", AutoSize = true, Margin = new Padding(0, 6, 0, 0) });
            options.Controls.Add(_units);
            options.Controls.Add(new Label { Text = "Tolerance:", AutoSize = true, Margin = new Padding(12, 6, 0, 0) });
            options.Controls.Add(_tolerance);
            options.Controls.Add(_run);
            options.Controls.Add(_save);
            options.Controls.Add(_apiKey);
            grid.Controls.Add(options, 0, 2);
            grid.SetColumnSpan(options, 3);

            grid.Controls.Add(_status, 0, 3);
            grid.SetColumnSpan(_status, 3);
            _status.Height = 22;
            return grid;
        }

        private Control BuildResultsArea()
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 300 };
            split.Panel1.Controls.Add(_checks);
            split.Panel1.Controls.Add(_summary);
            split.Panel1.Controls.Add(_headline);

            var fixesBox = new GroupBox { Text = "How to fix", Dock = DockStyle.Fill };
            fixesBox.Controls.Add(_fixes);
            split.Panel2.Controls.Add(fixesBox);

            var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 8, 8) };
            outer.Controls.Add(split);
            return outer;
        }

        private void BrowseItm()
        {
            var root = _items.ItemContentFolder;
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                using (var browser = new ItemBrowserForm(root!))
                {
                    if (browser.ShowDialog(this) == DialogResult.OK && browser.SelectedPath != null)
                        _itmPath.Text = browser.SelectedPath;
                }
            }
            else
            {
                using (var dialog = new OpenFileDialog { Filter = "Fabrication items (*.itm)|*.itm" })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        _itmPath.Text = dialog.FileName;
                }
            }
            UpdateRunEnabled();
        }

        private void BrowsePdf()
        {
            using (var dialog = new OpenFileDialog { Filter = "PDF submittals (*.pdf)|*.pdf" })
            {
                if (!string.IsNullOrEmpty(_pdfPath.Text))
                    dialog.InitialDirectory = Path.GetDirectoryName(_pdfPath.Text);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _pdfPath.Text = dialog.FileName;
            }
            UpdateRunEnabled();
        }

        private void UpdateRunEnabled() =>
            _run.Enabled = File.Exists(_itmPath.Text) && File.Exists(_pdfPath.Text);

        private async System.Threading.Tasks.Task RunAsync()
        {
            var apiKey = ApiKeyStore.Load() ?? PromptForApiKey();
            if (apiKey == null)
                return;

            ClearResults();
            _run.Enabled = false;
            _save.Enabled = false;
            UseWaitCursor = true;
            _cancel = new CancellationTokenSource();

            try
            {
                // Fabrication API calls stay on ESTmep's UI thread.
                SetStatus("Drawing every size of the ITM into the job…");
                var progress = new Progress<string>(SetStatus);
                var snapshot = _items.DrawAllSizes(_itmPath.Text, (string)_units.SelectedItem!, progress);
                _lastSnapshot = snapshot;

                if (snapshot.Sizes.Count == 0)
                {
                    ShowError("The ITM did not report any sizes to check.");
                    return;
                }

                SetStatus($"Drew {snapshot.Sizes.Count} size(s). Claude is reading the submittal and comparing dimensions…");
                var pdf = File.ReadAllBytes(_pdfPath.Text);
                var verifier = new ClaudeSubmittalVerifier(apiKey);
                var report = await verifier.VerifyAsync(snapshot, pdf, new VerificationOptions { Tolerance = _tolerance.Text }, _cancel.Token);

                _lastReport = report;
                ShowReport(report);
                SetStatus($"Checked {snapshot.Sizes.Count} size(s) against {Path.GetFileName(_pdfPath.Text)}.");
                _save.Enabled = true;
            }
            catch (OperationCanceledException)
            {
                SetStatus("Cancelled.");
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                UseWaitCursor = false;
                UpdateRunEnabled();
                _cancel.Dispose();
                _cancel = null;
            }
        }

        private void ShowReport(VerificationReport report)
        {
            _headline.Text = ReportFormatter.Headline(report);
            _headline.ForeColor = report.OverallStatus == "pass" ? Color.ForestGreen
                : report.OverallStatus == "fail" ? Color.Firebrick : Color.DarkOrange;

            var summary = report.Summary;
            if (!string.IsNullOrEmpty(report.SubmittalProduct))
                summary = "Submittal: " + report.SubmittalProduct + Environment.NewLine + summary;
            if (report.SizesMissingFromItm.Count > 0)
                summary += Environment.NewLine + "Sizes on the submittal missing from the ITM: " + string.Join(", ", report.SizesMissingFromItm);
            _summary.Text = summary;

            foreach (var size in report.Sizes)
            {
                if (size.Checks.Count == 0)
                {
                    _checks.Rows.Add(size.SizeName, "", "", "", "", size.Status, "");
                    continue;
                }
                foreach (var check in size.Checks)
                {
                    var row = _checks.Rows[_checks.Rows.Add(size.SizeName, check.ItmDimension, check.SubmittalLabel,
                        check.ItmValue, check.SubmittalValue, check.Status, check.Note)];
                    if (check.Status == "mismatch")
                        row.DefaultCellStyle.BackColor = Color.MistyRose;
                }
            }

            _fixes.Text = ReportFormatter.FixesText(report).Replace("\n", Environment.NewLine).Replace("\r\r", "\r");
        }

        private void ClearResults()
        {
            _headline.Text = "";
            _summary.Text = "";
            _checks.Rows.Clear();
            _fixes.Text = "";
            _lastReport = null;
        }

        private void ShowError(string message)
        {
            _headline.Text = "COULD NOT VERIFY";
            _headline.ForeColor = Color.DarkOrange;
            _summary.Text = message;
            SetStatus("Stopped with an error.");
        }

        private void SetStatus(string text) => _status.Text = text;

        private void SaveReport()
        {
            if (_lastReport == null || _lastSnapshot == null)
                return;

            using (var dialog = new SaveFileDialog
            {
                Filter = "Text report (*.txt)|*.txt",
                FileName = Path.GetFileNameWithoutExtension(_itmPath.Text) + " - submittal check.txt",
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    File.WriteAllText(dialog.FileName, ReportFormatter.FullText(_lastReport, _lastSnapshot, _pdfPath.Text));
            }
        }

        private string? PromptForApiKey()
        {
            using (var form = new Form
            {
                Text = "Anthropic API key", Size = new Size(460, 150), StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowIcon = false,
            })
            {
                var box = new TextBox { UseSystemPasswordChar = true, Dock = DockStyle.Top };
                var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
                form.Controls.Add(box);
                form.Controls.Add(new Label { Text = "Paste your key from console.anthropic.com. It is stored encrypted for your Windows account.", Dock = DockStyle.Top, Height = 36 });
                form.Controls.Add(ok);
                form.AcceptButton = ok;

                if (form.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(box.Text))
                    return null;

                ApiKeyStore.Save(box.Text);
                return box.Text.Trim();
            }
        }
    }
}
