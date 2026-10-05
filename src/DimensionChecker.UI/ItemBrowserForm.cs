using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EstMepAddins.DimensionChecker.UI
{
    /// <summary>
    /// Tree of the database's item folders, like ESTmep's own service/item browser, for picking one ITM.
    /// </summary>
    public sealed class ItemBrowserForm : Form
    {
        private const string Placeholder = "…";

        private readonly TreeView _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
        private readonly TextBox _search = new TextBox { Dock = DockStyle.Fill };
        private readonly Button _ok = new Button { Text = "Select", DialogResult = DialogResult.OK, Enabled = false, AutoSize = true };
        private readonly Button _other = new Button { Text = "Browse elsewhere…", AutoSize = true };
        private readonly Label _rootLabel = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        private readonly string _root;

        public string? SelectedPath { get; private set; }

        public ItemBrowserForm(string root)
        {
            _root = root;
            Text = "Select an ITM";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(520, 640);
            MinimizeBox = false;
            ShowIcon = false;
            AcceptButton = _ok;

            var searchRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 30, ColumnCount = 2, Padding = new Padding(6, 4, 6, 0) };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.Controls.Add(new Label { Text = "Find:", AutoSize = true, Anchor = AnchorStyles.Left });
            searchRow.Controls.Add(_search);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(6) };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(_ok);
            buttons.Controls.Add(_other);
            CancelButton = cancel;

            _rootLabel.Height = 22;
            _rootLabel.Dock = DockStyle.Top;
            _rootLabel.Padding = new Padding(6, 0, 6, 0);

            Controls.Add(_tree);
            Controls.Add(_rootLabel);
            Controls.Add(searchRow);
            Controls.Add(buttons);

            _tree.BeforeExpand += (s, e) => Populate(e.Node);
            _tree.AfterSelect += (s, e) => _ok.Enabled = IsItem(e.Node);
            _tree.NodeMouseDoubleClick += (s, e) =>
            {
                if (IsItem(e.Node)) { DialogResult = DialogResult.OK; Close(); }
            };
            _search.TextChanged += (s, e) => LoadTree();
            _other.Click += (s, e) => BrowseElsewhere();
            FormClosing += (s, e) =>
            {
                if (DialogResult == DialogResult.OK && SelectedPath == null)
                    SelectedPath = _tree.SelectedNode?.Tag as string;
            };

            LoadTree();
        }

        private static bool IsItem(TreeNode? node) =>
            node?.Tag is string path && path.EndsWith(".itm", StringComparison.OrdinalIgnoreCase);

        private void LoadTree()
        {
            _rootLabel.Text = _root;
            _tree.BeginUpdate();
            _tree.Nodes.Clear();

            if (Directory.Exists(_root))
            {
                var filter = _search.Text.Trim();
                if (filter.Length >= 2)
                    LoadSearchResults(filter);
                else
                    AddChildren(_tree.Nodes, _root);
            }

            _tree.EndUpdate();
        }

        private void LoadSearchResults(string filter)
        {
            var matches = SafeFiles(_root, SearchOption.AllDirectories)
                .Where(f => Path.GetFileNameWithoutExtension(f).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(500);
            foreach (var file in matches)
            {
                var relative = file.Substring(_root.Length).TrimStart(Path.DirectorySeparatorChar);
                _tree.Nodes.Add(new TreeNode(relative) { Tag = file });
            }
        }

        private void Populate(TreeNode node)
        {
            if (node.Nodes.Count == 1 && node.Nodes[0].Text == Placeholder)
            {
                node.Nodes.Clear();
                AddChildren(node.Nodes, (string)node.Tag);
            }
        }

        private static void AddChildren(TreeNodeCollection nodes, string folder)
        {
            foreach (var dir in SafeDirectories(folder).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var node = new TreeNode(Path.GetFileName(dir)) { Tag = dir };
                node.Nodes.Add(Placeholder);
                nodes.Add(node);
            }

            foreach (var file in SafeFiles(folder, SearchOption.TopDirectoryOnly).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                nodes.Add(new TreeNode(Path.GetFileNameWithoutExtension(file)) { Tag = file });
        }

        private static string[] SafeDirectories(string folder)
        {
            try { return Directory.GetDirectories(folder); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return new string[0]; }
        }

        private static string[] SafeFiles(string folder, SearchOption option)
        {
            try { return Directory.GetFiles(folder, "*.itm", option); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return new string[0]; }
        }

        private void BrowseElsewhere()
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "Fabrication items (*.itm)|*.itm",
                InitialDirectory = Directory.Exists(_root) ? _root : "",
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                SelectedPath = dialog.FileName;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
