using DendroKit.Core.Params;

namespace DendroKit.View;

/// <summary>
/// Left panel: group tree + parameter editing grid.
/// </summary>
public sealed class ParamPanel : UserControl
{
    private readonly TreeView     _groupTree;
    private readonly DataGridView _grid;
    private readonly SplitContainer _inner;

    private TreeParams? _params;
    private string?     _activeGroup;

    public ParamPanel()
    {
        Dock = DockStyle.Fill;

        _inner = new SplitContainer
        {
            Dock        = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 180,
        };

        _groupTree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
        _groupTree.AfterSelect += OnGroupSelect;
        _inner.Panel1.Controls.Add(_groupTree);

        _grid = new DataGridView
        {
            Dock                  = DockStyle.Fill,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible     = false,
            AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect           = false,
        };
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Paraméter", Name = "colName", ReadOnly = true, FillWeight = 55,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Érték", Name = "colValue", ReadOnly = false, FillWeight = 30,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Alapért.", Name = "colDefault", ReadOnly = true, FillWeight = 25,
        });
        _grid.CellEndEdit += OnCellEdit;
        _inner.Panel2.Controls.Add(_grid);

        Controls.Add(_inner);
    }

    public void LoadParams(TreeParams p)
    {
        _params = p;
        _groupTree.Nodes.Clear();

        var groups = p.ParamDb.Values
                      .Where(x => x.Enabled)
                      .GroupBy(x => x.Group ?? "Általános")
                      .OrderBy(g => g.First().Order);

        foreach (var g in groups)
        {
            var node = new TreeNode(g.Key) { Tag = g.Key };
            _groupTree.Nodes.Add(node);
        }

        if (_groupTree.Nodes.Count > 0)
        {
            _groupTree.SelectedNode = _groupTree.Nodes[0];
        }
    }

    public void ApplyChanges()
    {
        // changes are applied cell-by-cell in OnCellEdit; nothing extra needed
    }

    private void OnGroupSelect(object? sender, TreeViewEventArgs e)
    {
        if (_params == null || e.Node?.Tag is not string group) return;
        _activeGroup = group;
        RefreshGrid(group);
    }

    private void RefreshGrid(string group)
    {
        if (_params == null) return;
        _grid.Rows.Clear();

        var paramList = _params.ParamDb.Values
                               .Where(p => p.Enabled && (p.Group ?? "Általános") == group)
                               .OrderBy(p => p.Order);

        foreach (var p in paramList)
        {
            int i = _grid.Rows.Add();
            _grid.Rows[i].Tag = p;
            _grid.Rows[i].Cells["colName"].Value    = p.Name;
            _grid.Rows[i].Cells["colValue"].Value   = p.GetValue();
            _grid.Rows[i].Cells["colDefault"].Value = p.GetDefaultValue();
        }
    }

    private void OnCellEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.ColumnIndex != _grid.Columns["colValue"]!.Index) return;
        if (_grid.Rows[e.RowIndex].Tag is not AbstractParam param) return;

        var raw = _grid.Rows[e.RowIndex].Cells["colValue"].Value?.ToString() ?? "";
        try
        {
            param.SetValue(raw);
        }
        catch
        {
            // revert on bad input
            _grid.Rows[e.RowIndex].Cells["colValue"].Value = param.GetValue();
        }
    }
}
