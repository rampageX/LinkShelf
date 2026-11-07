using System.Runtime.InteropServices;
namespace LinkShelf.UI;
public static class Dark
{
    public static readonly Color Back = Color.FromArgb(22,24,28), Surface = Color.FromArgb(31,34,40), Raised = Color.FromArgb(41,45,53), Text = Color.FromArgb(226,229,234), Muted = Color.FromArgb(155,164,179), Accent = Color.FromArgb(95,205,162);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string subapp, string? subid);
    public static void Apply(Control c)
    {
        c.BackColor = c is TextBoxBase or ComboBox or DarkComboBox ? Raised : Back; c.ForeColor = Text;
        if (c is Form f) { f.Font = new Font("Microsoft YaHei UI", 9F); f.StartPosition = FormStartPosition.CenterParent; }
        if (c is Button b) { b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Raised; b.BackColor = Surface; b.Padding = new Padding(8,0,8,0); b.Cursor = Cursors.Hand; }
        if (c is TextBox t) t.BorderStyle = BorderStyle.FixedSingle;
        if (c is ComboBox combo)
        {
            combo.FlatStyle = FlatStyle.Flat;
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.DrawItem += (_, e) =>
            {
                bool selected = (e.State & DrawItemState.Selected) != 0;
                using var brush = new SolidBrush(selected ? Color.FromArgb(43,80,69) : Raised);
                e.Graphics.FillRectangle(brush,e.Bounds);
                string label = e.Index >= 0 ? combo.GetItemText(combo.Items[e.Index]) ?? "" : combo.Text;
                TextRenderer.DrawText(e.Graphics,label,e.Font ?? combo.Font,Rectangle.Inflate(e.Bounds,-4,0),combo.Enabled ? Text : Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            };
        }
        foreach (Control child in c.Controls) Apply(child);
        c.HandleCreated += (_, _) =>
        {
            if (c is Form) { int yes = 1; DwmSetWindowAttribute(c.Handle, 20, ref yes, 4); }
            if (c is TextBoxBase or ComboBox or ListView or ScrollBar) SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
        };
    }
    public static Button Button(string label, EventHandler click, int width = 110)
    { var b = new DarkButton { Text = label, AutoSize = false, Width = width, Height = 34, Margin = new Padding(0,0,8,0) }; b.Click += click; return b; }
    public static DialogResult Message(IWin32Window? owner, string title, string text, bool confirm = false)
    {
        using var f = new DarkForm { Text = title, ClientSize = new Size(520,250), MinimumSize = new Size(450,250), FormBorderStyle = FormBorderStyle.Sizable, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false };
        var layout = new DarkTableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
        var content = new TextBox { Text = text, Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None };
        var buttons = new DarkFlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = Button(confirm ? "取消" : "知道了", (_,_) => { f.DialogResult = DialogResult.Cancel; }, 90); buttons.Controls.Add(cancel); f.CancelButton = cancel;
        if (confirm) { var ok = Button("确认", (_,_) => { f.DialogResult = DialogResult.OK; }, 90); buttons.Controls.Add(ok); f.AcceptButton = ok; }
        layout.Controls.Add(content,0,0); layout.Controls.Add(buttons,0,1); f.Controls.Add(layout); Apply(f);
        return owner == null ? f.ShowDialog() : f.ShowDialog(owner);
    }
    public static void ShowError(IWin32Window? owner, string text) => Message(owner, "操作未完成", text);
}
public sealed class DarkForm : Form
{
    public DarkForm() => DoubleBuffered = true;
}
public sealed class DarkTableLayoutPanel : TableLayoutPanel
{
    public DarkTableLayoutPanel() => SetStyle(ControlStyles.OptimizedDoubleBuffer |
        ControlStyles.AllPaintingInWmPaint, true);
}
public sealed class DarkFlowLayoutPanel : FlowLayoutPanel
{
    public DarkFlowLayoutPanel() => SetStyle(ControlStyles.OptimizedDoubleBuffer |
        ControlStyles.AllPaintingInWmPaint, true);
}
// WinForms native disabled-button text becomes nearly black on dark backgrounds.
// Owner-paint every application button so its disabled label remains readable.
public sealed class DarkButton : Button
{
    bool hovering, pressed;
    public DarkButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        UseVisualStyleBackColor = false;
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovering = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovering = false; pressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); pressed = false; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Color background = !Enabled ? Dark.Surface : pressed ? Color.FromArgb(51, 74, 68)
                         : hovering ? Dark.Raised : Dark.Surface;
        e.Graphics.Clear(background);
        using var border = new Pen(Focused && Enabled ? Dark.Accent : Dark.Raised);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? Dark.Text : Dark.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}
public sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new Colors()) { }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = Dark.Text; base.OnRenderItemText(e); }
    sealed class Colors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Dark.Surface;
        public override Color ImageMarginGradientBegin => Dark.Surface;
        public override Color ImageMarginGradientMiddle => Dark.Surface;
        public override Color ImageMarginGradientEnd => Dark.Surface;
        public override Color MenuItemSelected => Dark.Raised;
        public override Color MenuItemBorder => Dark.Raised;
        public override Color MenuBorder => Dark.Raised;
    }
}
