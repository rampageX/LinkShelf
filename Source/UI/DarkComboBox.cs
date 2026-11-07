using System.Collections.ObjectModel;
using System.Drawing.Drawing2D;

namespace LinkShelf.UI;

// The native Win32 ComboBox still paints a bright border and arrow on some
// Windows themes. Paint the closed control ourselves and use a dark list popup.
public sealed class DarkComboBox : Control
{
    public sealed class ItemCollection : Collection<object>
    {
        readonly DarkComboBox owner;
        internal ItemCollection(DarkComboBox owner) => this.owner = owner;
        public void AddRange(object[] items) { foreach (object item in items) Add(item); }
        protected override void InsertItem(int index, object item) { base.InsertItem(index, item); owner.Invalidate(); }
        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            if (owner.selectedIndex >= Count) owner.SelectedIndex = Count - 1;
            owner.Invalidate();
        }
        protected override void ClearItems() { base.ClearItems(); owner.SelectedIndex = -1; owner.Invalidate(); }
        protected override void SetItem(int index, object item) { base.SetItem(index, item); owner.Invalidate(); }
    }

    readonly ItemCollection items;
    ToolStripDropDown? popup;
    ListBox? popupList;
    ToolStripControlHost? popupHost;
    int selectedIndex = -1;

    public ItemCollection Items => items;
    public object? SelectedItem => selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null;
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value < -1 || value >= items.Count) throw new ArgumentOutOfRangeException(nameof(value));
            if (selectedIndex == value) return;
            selectedIndex = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? SelectedIndexChanged;

    public DarkComboBox()
    {
        items = new ItemCollection(this);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        BackColor = Dark.Raised;
        ForeColor = Dark.Text;
        TabStop = true;
        AccessibleRole = AccessibleRole.ComboBox;
    }

    protected override Size DefaultSize => new(160, 27);

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (Width < 4 || Height < 4) return;

        float scale = DeviceDpi / 96f;
        int arrowWidth = Math.Max(24, (int)Math.Round(27 * scale));
        int arrowLeft = Math.Max(1, Width - arrowWidth);
        using (var arrowBack = new SolidBrush(Dark.Surface))
            e.Graphics.FillRectangle(arrowBack, arrowLeft, 1, Width - arrowLeft - 1, Height - 2);
        using (var border = new Pen(Focused && Enabled ? Dark.Accent : Color.FromArgb(93, 101, 112)))
            e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using (var divider = new Pen(Color.FromArgb(73, 81, 92)))
            e.Graphics.DrawLine(divider, arrowLeft, 1, arrowLeft, Height - 2);

        int inset = Math.Max(7, (int)Math.Round(8 * scale));
        var textBounds = new Rectangle(inset, 1, Math.Max(0, arrowLeft - inset - 5), Height - 2);
        TextRenderer.DrawText(e.Graphics, SelectedItem?.ToString() ?? "", Font, textBounds,
            Enabled ? ForeColor : Dark.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int cx = (arrowLeft + Width) / 2, cy = Height / 2;
        int radius = Math.Max(3, (int)Math.Round(4 * scale));
        using var arrow = new Pen(Enabled ? Dark.Text : Dark.Muted, Math.Max(1.5f, 1.5f * scale))
        { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        e.Graphics.DrawLines(arrow,
            new Point[] { new(cx - radius, cy - radius / 2), new(cx, cy + radius / 2), new(cx + radius, cy - radius / 2) });
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && Enabled) { Focus(); OpenPopup(); }
    }

    protected override bool IsInputKey(Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        return key is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.F4 || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Enabled || items.Count == 0) return;
        if (e.KeyCode is Keys.F4 or Keys.Space || e.Alt && e.KeyCode == Keys.Down)
            OpenPopup();
        else if (e.KeyCode == Keys.Down) SelectedIndex = Math.Min(items.Count - 1, selectedIndex + 1);
        else if (e.KeyCode == Keys.Up) SelectedIndex = Math.Max(0, selectedIndex - 1);
        else if (e.KeyCode == Keys.Home) SelectedIndex = 0;
        else if (e.KeyCode == Keys.End) SelectedIndex = items.Count - 1;
        else return;
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (!Enabled || char.IsControl(e.KeyChar)) return;
        for (int i = 0; i < items.Count; i++)
            if ((items[i]?.ToString() ?? "").StartsWith(e.KeyChar.ToString(), StringComparison.CurrentCultureIgnoreCase))
            { SelectedIndex = i; e.Handled = true; break; }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Enabled && popup?.Visible != true && items.Count > 0)
            SelectedIndex = Math.Clamp(selectedIndex - Math.Sign(e.Delta), 0, items.Count - 1);
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    void OpenPopup()
    {
        if (popup?.Visible == true || !Enabled || items.Count == 0) return;
        int itemHeight = Math.Max(Font.Height + 10, (int)Math.Round(27 * DeviceDpi / 96f));
        int count = Math.Min(items.Count, 10);
        int listHeight = count * itemHeight;
        int popupWidth = Math.Max(Width, 100);
        // Create the popup only once. ToolStrip's modal-menu filter may still
        // refer to a closed dropdown during the same mouse message, so it must
        // never be disposed from its Closed event.
        if (popup == null)
        {
            var list = new ListBox
            {
                BorderStyle = BorderStyle.None, BackColor = Dark.Raised, ForeColor = Dark.Text,
                Font = Font, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = itemHeight,
                IntegralHeight = false, Size = new Size(popupWidth - 2, listHeight)
            };
            list.DrawItem += (_, e) =>
            {
                if (e.Index < 0) return;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                using var fill = new SolidBrush(selected ? Color.FromArgb(43, 80, 69) : Dark.Raised);
                e.Graphics.FillRectangle(fill, e.Bounds);
                TextRenderer.DrawText(e.Graphics, list.Items[e.Index]?.ToString() ?? "", Font,
                    Rectangle.Inflate(e.Bounds, -7, 0), Dark.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            var drop = new ToolStripDropDown
            {
                AutoSize = false, Padding = new Padding(1), Margin = Padding.Empty,
                BackColor = Dark.Surface, Renderer = new DarkMenuRenderer()
            };
            var host = new ToolStripControlHost(list)
            {
                AutoSize = false, Padding = Padding.Empty, Margin = Padding.Empty
            };
            drop.Items.Add(host);
            popup = drop; popupList = list; popupHost = host;
            void Commit()
            {
                if (list.SelectedIndex >= 0) SelectedIndex = list.SelectedIndex;
                drop.Close();
                Focus();
            }
            list.MouseClick += (_, e) => { if (list.IndexFromPoint(e.Location) >= 0) Commit(); };
            list.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { Commit(); e.Handled = true; }
                else if (e.KeyCode == Keys.Escape) { drop.Close(); Focus(); e.Handled = true; }
            };
            drop.Closed += (_, _) => { if (!IsDisposed && !Disposing) Invalidate(); };
        }
        var activeList = popupList!;
        activeList.Font = Font;
        activeList.ItemHeight = itemHeight;
        activeList.Items.Clear();
        foreach (var item in items) activeList.Items.Add(item);
        activeList.SelectedIndex = selectedIndex;
        activeList.Size = new Size(popupWidth - 2, listHeight);
        popupHost!.Size = activeList.Size;
        popup!.Size = new Size(popupWidth, listHeight + 2);
        popup.Show(this, new Point(0, Height));
        activeList.Focus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { popup?.Dispose(); popup = null; }
        base.Dispose(disposing);
    }
}
