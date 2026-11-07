using LinkShelf.Core;
namespace LinkShelf.UI;
public sealed class LinkDialog : Form
{
    readonly TextBox link = new() { Dock = DockStyle.Fill }, target = new() { Dock = DockStyle.Fill };
    readonly DarkComboBox kind = new() { Dock = DockStyle.Fill };
    public string LinkPath => System.IO.Path.GetFullPath(link.Text.Trim()).TrimEnd('\\');
    public string TargetPath => target.Text.Trim();
    public uint LinkTag => kind.SelectedIndex == 1 ? Reparse.Symlink : Reparse.Mount;
    public LinkDialog(LinkRow? row = null)
    {
        Text = row == null ? "新建目录链接" : "修改链接指向";
        ClientSize = new Size(640,280); MinimumSize = new Size(600,320); MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        DoubleBuffered = true;
        var table = new DarkTableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 3, RowCount = 5 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,88));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute,40)); table.RowStyles.Add(new RowStyle(SizeType.Absolute,40)); table.RowStyles.Add(new RowStyle(SizeType.Absolute,40)); table.RowStyles.Add(new RowStyle(SizeType.Percent,100)); table.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
        table.Controls.Add(new Label { Text = "链接路径", AutoSize = true, Anchor = AnchorStyles.Left },0,0); table.Controls.Add(link,1,0); table.SetColumnSpan(link,2);
        table.Controls.Add(new Label { Text = "目标目录", AutoSize = true, Anchor = AnchorStyles.Left },0,1); table.Controls.Add(target,1,1);
        table.Controls.Add(Dark.Button("浏览…", (_,_) => { using var d = new FolderBrowserDialog { Description = "选择已有目标目录", UseDescriptionForTitle = true }; if (d.ShowDialog(this) == DialogResult.OK) target.Text = d.SelectedPath; },80),2,1);
        table.Controls.Add(new Label { Text = "链接类型", AutoSize = true, Anchor = AnchorStyles.Left },0,2);
        kind.Items.AddRange(["目录连接点 /J（本地目录）", "目录符号链接 /D（支持网络路径）"]); kind.SelectedIndex = 0; table.Controls.Add(kind,1,2); table.SetColumnSpan(kind,2);
        var note = new Label { Text = "新建时链接路径必须尚不存在；目标目录必须已存在。\n修改指向只改变链接关系，不移动任何文件。符号链接可能需要开发者模式或管理员权限。", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0,8,0,0) }; table.Controls.Add(note,0,3); table.SetColumnSpan(note,3);
        var buttons = new DarkFlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = Dark.Button("取消", (_,_) => DialogResult = DialogResult.Cancel,90); var ok = Dark.Button(row == null ? "创建" : "预览修改", (_,_) =>
        {
            try { _ = LinkPath; _ = Reparse.ValidateTarget(TargetPath, LinkPath, LinkTag); DialogResult = DialogResult.OK; }
            catch (Exception e) { Dark.ShowError(this,e.Message); }
        },100);
        buttons.Controls.Add(cancel); buttons.Controls.Add(ok); AcceptButton = ok; CancelButton = cancel; table.Controls.Add(buttons,0,4); table.SetColumnSpan(buttons,3);
        if (row != null) { link.Text = row.Path; link.ReadOnly = true; target.Text = row.Target; kind.SelectedIndex = row.Tag == Reparse.Symlink ? 1 : 0; kind.Enabled = false; }
        Controls.Add(table); Dark.Apply(this); note.ForeColor = Dark.Muted;
    }
}
