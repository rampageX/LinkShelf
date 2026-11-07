namespace LinkShelf.UI;
// A painted scrollbar avoids native white scrollbar tracks on Windows themes.
public sealed class DarkScrollBar : Control
{
    readonly bool vertical;
    int maximum, currentValue, viewport = 1, dragOrigin, dragValue;
    bool dragging;
    public event EventHandler? ValueChanged;
    public int Maximum { get => maximum; set { maximum = Math.Max(0,value); Value = currentValue; Invalidate(); } }
    public int Viewport { get => viewport; set { viewport=Math.Max(1,value);Invalidate(); } }
    public int Value { get => currentValue; set { int v=Math.Clamp(value,0,maximum);if(v==currentValue)return;currentValue=v;Invalidate();ValueChanged?.Invoke(this,EventArgs.Empty); } }
    public DarkScrollBar(bool vertical)
    {
        this.vertical=vertical;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);
        BackColor=Dark.Back;Cursor=Cursors.Default;TabStop=false;
    }
    int Length=>vertical?ClientSize.Height:ClientSize.Width;
    int ThumbLength=>maximum==0?Length:Math.Min(Length,Math.Max(26,(int)((long)Length*viewport/(maximum+viewport))));
    int Travel=>Math.Max(0,Length-ThumbLength);
    int ThumbStart=>maximum==0?0:(int)((long)currentValue*Travel/maximum);
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Dark.Back);if(maximum==0)return;
        using var brush=new SolidBrush(dragging?Dark.Accent:Color.FromArgb(83,91,103));
        var rect=vertical?new Rectangle(3,ThumbStart+2,Math.Max(1,Width-6),Math.Max(1,ThumbLength-4)):new Rectangle(ThumbStart+2,3,Math.Max(1,ThumbLength-4),Math.Max(1,Height-6));
        e.Graphics.FillRectangle(brush,rect);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||maximum==0)return;
        int pos=vertical?e.Y:e.X;
        if(pos>=ThumbStart&&pos<=ThumbStart+ThumbLength){dragging=true;dragOrigin=pos;dragValue=currentValue;Capture=true;Invalidate();}
        else Value+=pos<ThumbStart?-viewport:viewport;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {base.OnMouseMove(e);if(dragging&&Travel>0)Value=dragValue+(int)((long)((vertical?e.Y:e.X)-dragOrigin)*maximum/Travel);}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;Invalidate();}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){dragging=false;Invalidate();}}
}
public sealed class DarkGridHost : TableLayoutPanel
{
    readonly DataGridView grid;
    readonly DarkScrollBar vertical=new(true){Dock=DockStyle.Fill,Margin=Padding.Empty};
    readonly DarkScrollBar horizontal=new(false){Dock=DockStyle.Fill,Margin=Padding.Empty};
    bool syncing;
    public DarkGridHost(DataGridView grid)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
        this.grid=grid;Dock=DockStyle.Fill;Margin=Padding.Empty;ColumnCount=2;RowCount=2;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,15));
        RowStyles.Add(new RowStyle(SizeType.Percent,100));RowStyles.Add(new RowStyle(SizeType.Absolute,15));
        grid.ScrollBars=ScrollBars.None;grid.Margin=Padding.Empty;
        Controls.Add(grid,0,0);Controls.Add(vertical,1,0);Controls.Add(horizontal,0,1);
        vertical.ValueChanged+=(_,_)=>{if(!syncing&&grid.Rows.Count>0){try{grid.FirstDisplayedScrollingRowIndex=Math.Min(vertical.Value,grid.Rows.Count-1);}catch(InvalidOperationException){}}};
        horizontal.ValueChanged+=(_,_)=>{if(!syncing)grid.HorizontalScrollingOffset=horizontal.Value;};
        grid.Scroll+=(_,_)=>Sync();grid.SizeChanged+=(_,_)=>Sync();grid.RowsAdded+=(_,_)=>Sync();grid.RowsRemoved+=(_,_)=>Sync();
        grid.ColumnWidthChanged+=(_,_)=>Sync();grid.DataBindingComplete+=(_,_)=>Sync();
        grid.MouseWheel+=(_,e)=>{if(e is HandledMouseEventArgs handled)handled.Handled=true;vertical.Value-=Math.Sign(e.Delta)*Math.Max(1,SystemInformation.MouseWheelScrollLines);};
        grid.HandleCreated+=(_,_)=>Sync();
    }
    void Sync()
    {
        if(syncing||IsDisposed||Disposing||grid.IsDisposed||grid.Disposing)return;syncing=true;
        try
        {
            int visible=Math.Max(1,(grid.ClientSize.Height-grid.ColumnHeadersHeight)/Math.Max(1,grid.RowTemplate.Height));
            vertical.Viewport=visible;vertical.Maximum=Math.Max(0,grid.Rows.Count-visible);vertical.Value=Math.Max(0,grid.FirstDisplayedScrollingRowIndex);
            int width=grid.Columns.Cast<DataGridViewColumn>().Where(c=>c.Visible).Sum(c=>c.Width);
            bool horizontalNeeded=width>grid.ClientSize.Width+1;
            if(horizontal.Visible!=horizontalNeeded)
            {
                horizontal.Visible=horizontalNeeded;
                RowStyles[1].Height=horizontalNeeded?15:0;
                // The grid receives extra height after the row collapses.
                if(IsHandleCreated && !IsDisposed && !Disposing)
                    BeginInvoke(new Action(()=>{if(!IsDisposed&&!Disposing)Sync();}));
            }
            horizontal.Viewport=Math.Max(1,grid.ClientSize.Width);
            horizontal.Maximum=Math.Max(0,width-grid.ClientSize.Width);
            horizontal.Value=grid.HorizontalScrollingOffset;
        }
        finally{syncing=false;}
    }
}
public sealed class DarkCheckBox : CheckBox
{
    public DarkCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);int side=Math.Max(13,(int)(13*DeviceDpi/96f));int y=(Height-side)/2;
        using var fill=new SolidBrush(Dark.Raised);using var outline=new Pen(Enabled?Dark.Muted:Dark.Raised);
        e.Graphics.FillRectangle(fill,0,y,side,side);e.Graphics.DrawRectangle(outline,0,y,side-1,side-1);
        if(Checked){using var check=new Pen(Dark.Accent,Math.Max(2,DeviceDpi/48f));e.Graphics.DrawLines(check,new Point[]{new Point(side/5,y+side/2),new Point(side*2/5,y+side*3/4),new Point(side*4/5,y+side/4)});}
        var rect=new Rectangle(side+6,0,Math.Max(0,Width-side-6),Height);
        TextRenderer.DrawText(e.Graphics,Text,Font,rect,Enabled?ForeColor:Dark.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,rect,Dark.Muted,BackColor);
    }
}
