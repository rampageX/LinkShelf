using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LinkShelf.Core;
namespace LinkShelf.UI;
public sealed class MainForm : Form
{
    [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr wparam, IntPtr lparam);
    readonly Settings settings = Settings.Load();
    readonly DarkComboBox instances = new() { Width = 180 };
    readonly TextBox search = new() { Width = 250, PlaceholderText = "搜索名称、链接或目标路径" };
    readonly DarkCheckBox hideSystem = new() { Text = "隐藏系统属性链接", AutoSize = true, Checked = true };
    readonly DarkCheckBox onlyC = new() { Text = "仅 C 盘转出", AutoSize = true };
    readonly DarkComboBox state = new() { Width = 145 };
    readonly DataGridView grid = new();
    readonly Label total = new() { Text = "C 盘已转出数据  —", AutoSize = true, Font = new Font("Microsoft YaHei UI",18,FontStyle.Bold) };
    readonly Label summary = new() { Text = "连接正在运行的 Everything，读取已有索引", AutoSize = true };
    readonly Label status = new() { Text = "就绪", Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly TextBox detail = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    readonly List<LinkRow> rows = [];
    readonly Dictionary<string,SizeResult> cache = new(StringComparer.OrdinalIgnoreCase);
    readonly List<Button> actions = [];
    readonly Button stop;
    CancellationTokenSource? work;
    TaskCompletionSource<byte[]>? reply;
    IntPtr expectedSender;
    uint queryId = 0x4C530100;
    bool busy, firstShown = true, rendering;
    string sort = "Path"; bool ascending = true;
    readonly System.Windows.Forms.Timer filterTimer = new() { Interval = 180 };
    public MainForm()
    {
        Text = "LinkShelf · 目录链接管理  0.1.4";
        MinimumSize = new Size(980,620); ClientSize = new Size(1220,760);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        var root = new DarkTableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18,16,18,10), ColumnCount = 1, RowCount = 7 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,44)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,46)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,30));
        root.Controls.Add(total,0,0); root.Controls.Add(summary,0,1);
        var toolbar = new DarkFlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0,4,0,0) };
        toolbar.Controls.Add(instances);
        AddButton(toolbar,"连接 / 刷新", async (_,_) => await RefreshLinks());
        AddButton(toolbar,"计算当前列表", async (_,_) => await Calculate(VisibleRows(), false),125);
        AddButton(toolbar,"新建链接", (_,_) => CreateLink());
        AddButton(toolbar,"导出清单", (_,_) => Export());
        stop = Dark.Button("停止", (_,_) => work?.Cancel(),75); stop.Enabled = false; toolbar.Controls.Add(stop);
        var help = Dark.Button("说明", (_,_) => Dark.Message(this,"统计与操作说明",
            "链接发现直接查询 Everything 当前实例的已有索引，不重建索引。范围取决于该实例的索引设置。\r\n\r\n计算当前列表：扫描筛选后的目标目录，缓存只在本次程序运行期间复用；右键可以强制重算。不会跟随目标内部的重解析点。网络目标暂不扫描。\r\n\r\nC 盘已转出数据：按所有已发现的非系统属性链接计算，与搜索筛选无关；只累计 C: 链接指向其他本地盘的当前逻辑文件大小。相同目标和嵌套目标去重。未计算和部分结果会明确标出。\r\n\r\n这是当前数据量估算，不是历史释放空间或物理占用：压缩、稀疏文件、硬链接会造成差异。扫描过程中文件变化也可能影响结果。\r\n\r\n删除仅删除链接本身；修改指向不移动文件。所有写操作前记录 LinkShelf.operations.jsonl。卷挂载点与带系统属性的链接只读。\r\n\r\n首版不搬迁普通文件夹。请先退出使用该目录的软件，再用已有方式搬迁并建立链接，随后刷新核对。"),70); toolbar.Controls.Add(help);
        root.Controls.Add(toolbar,0,2);
        var filters = new DarkFlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0,4,0,0) };
        state.Items.AddRange(["全部状态", "尚未计算", "正常", "异常 / 部分"]); state.SelectedIndex = 0;
        filters.Controls.Add(search); filters.Controls.Add(hideSystem); filters.Controls.Add(onlyC); filters.Controls.Add(state); root.Controls.Add(filters,0,3);
        ConfigureGrid(); root.Controls.Add(new DarkGridHost(grid),0,4);
        root.Controls.Add(detail,0,5); root.Controls.Add(status,0,6); Controls.Add(root);
        hideSystem.Checked = settings.HideSystem;
        sort = new[]{"Name","Path","Kind","Target","Bytes","Status"}.Contains(settings.SortColumn) ? settings.SortColumn : "Path";
        ascending = settings.SortAscending;
        filterTimer.Tick += (_,_) => { filterTimer.Stop(); Render(); };
        search.TextChanged += (_,_) => { filterTimer.Stop(); filterTimer.Start(); };
        hideSystem.CheckedChanged += (_,_) => Render(); onlyC.CheckedChanged += (_,_) => Render(); state.SelectedIndexChanged += (_,_) => Render();
        Dark.Apply(this); total.ForeColor = Dark.Accent; summary.ForeColor = Dark.Muted; status.ForeColor = Dark.Muted;
        ConfigureGridColors();
        Shown += async (_,_) =>
        {
            if (!firstShown) return; firstShown = false;
            RestoreWindow(); DetectInstances();
            if (instances.Items.Count > 0) await RefreshLinks();
            else status.Text = "未发现 Everything 客户端。请先运行已有副本，然后点击“连接 / 刷新”。";
        };
        FormClosing += (_,e) =>
        {
            if (busy) { work?.Cancel(); e.Cancel = true; status.Text = "正在停止，请等待当前文件系统调用返回后再关闭。"; return; }
            try
            {
                var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                settings.X = b.X; settings.Y = b.Y; settings.Width = b.Width; settings.Height = b.Height;
                settings.Maximized = WindowState == FormWindowState.Maximized;
                settings.HideSystem = hideSystem.Checked; settings.Instance = (instances.SelectedItem as EverythingInstance)?.ClassName ?? "";
                settings.SortColumn = sort; settings.SortAscending = ascending;
                settings.ColumnWidths = grid.Columns.Cast<DataGridViewColumn>().Select(c=>c.Width).ToArray();
                settings.ColumnLayoutVersion = 1; settings.Save();
            }
            catch (Exception ex) { Dark.ShowError(this,"窗口设置未能保存：" + ex.Message); }
            filterTimer.Dispose();
        };
    }
    void AddButton(FlowLayoutPanel panel, string name, EventHandler action, int width = 110)
    { var b = Dark.Button(name,action,width); panel.Controls.Add(b); actions.Add(b); }
    void RestoreWindow()
    {
        var b = new Rectangle(settings.X,settings.Y,Math.Max(980,settings.Width),Math.Max(620,settings.Height));
        var screen = Screen.AllScreens.FirstOrDefault(s=>s.WorkingArea.IntersectsWith(new Rectangle(b.X,b.Y,b.Width,40))) ?? Screen.PrimaryScreen!;
        var wa = screen.WorkingArea;
        b.Width = Math.Min(b.Width,wa.Width); b.Height = Math.Min(b.Height,wa.Height);
        b.X = Math.Clamp(b.X,wa.Left,Math.Max(wa.Left,wa.Right-b.Width)); b.Y = Math.Clamp(b.Y,wa.Top,Math.Max(wa.Top,wa.Bottom-b.Height));
        Bounds = b; if (settings.Maximized) WindowState = FormWindowState.Maximized;
        // The old settings used absolute widths which could exceed the window.
        // Preserve the five flexible columns as proportions instead.
        if (settings.ColumnWidths.Length == grid.Columns.Count)
        {
            for (int i=0;i<grid.Columns.Count-1;i++)
                grid.Columns[i].FillWeight = Math.Clamp(settings.ColumnWidths[i], grid.Columns[i].MinimumWidth, 2000);
            // One-time migration: replace the old, oversized Status default.
            if (settings.ColumnLayoutVersion >= 1)
                grid.Columns["Status"].Width = Math.Clamp(settings.ColumnWidths[^1], 110, 400);
        }
    }
    void ConfigureGrid()
    {
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false; grid.RowHeadersVisible = false; grid.MultiSelect = true;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.AutoGenerateColumns = false;
        grid.BorderStyle = BorderStyle.None; grid.EnableHeadersVisualStyles = false; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersHeight = 34; grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.RowTemplate.Height = 29; grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // Enable double buffering once; do not recreate the grid on each refresh.
        typeof(DataGridView).GetProperty("DoubleBuffered",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(grid,true);
        void Column(string id,string name,int width,int minimum,float weight) => grid.Columns.Add(new DataGridViewTextBoxColumn
        { Name=id,HeaderText=name,Width=width,MinimumWidth=minimum,FillWeight=weight,SortMode=DataGridViewColumnSortMode.Programmatic });
        Column("Name","名称",130,95,12); Column("Path","链接完整路径",310,185,32);
        Column("Kind","类型",120,105,11); Column("Target","目标目录",320,195,34);
        Column("Bytes","目标大小",120,100,11); Column("Status","状态",145,110,10);
        // Status stays compact on wide screens; spare space goes to paths.
        grid.Columns["Status"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        grid.Columns["Status"]!.Width = 145;
        grid.Columns["Bytes"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.CellPainting += (_,e) =>
        {
            if (e.RowIndex != -1 || e.ColumnIndex < 0) return;
            e.Paint(e.CellBounds, DataGridViewPaintParts.All);
            int inset = Math.Max(5, (int)Math.Round(6 * DeviceDpi / 96f));
            using var separator = new Pen(Color.FromArgb(92,100,112));
            int x=e.CellBounds.Right-1;
            if(e.Graphics is Graphics graphics)
                graphics.DrawLine(separator,x,e.CellBounds.Top+inset,x,e.CellBounds.Bottom-inset);
            e.Handled = true;
        };
        grid.SelectionChanged += (_,_) => { if (!rendering) ShowDetail(); };
        grid.CellDoubleClick += (_,e) => { if(e.RowIndex>=0) OpenTarget(); };
        grid.ColumnHeaderMouseClick += (_,e) => { string id=grid.Columns[e.ColumnIndex].Name; ascending=sort==id?!ascending:true; sort=id; Render(); };
        grid.CellMouseDown += (_,e) => { if(e.Button==MouseButtons.Right && e.RowIndex>=0 && !grid.Rows[e.RowIndex].Selected) { grid.ClearSelection(); grid.Rows[e.RowIndex].Selected=true; grid.CurrentCell=grid.Rows[e.RowIndex].Cells[0]; } };
        var menu = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), BackColor=Dark.Surface, ForeColor=Dark.Text };
        void Item(string label,EventHandler click) => menu.Items.Add(label,null,click);
        Item("打开目标目录",(_,_)=>OpenTarget()); Item("定位链接",(_,_)=>Locate());
        Item("复制链接路径",(_,_)=>Copy(false)); Item("复制目标路径",(_,_)=>Copy(true)); menu.Items.Add(new ToolStripSeparator());
        Item("计算所选大小",async (_,_)=>await Calculate(SelectedRows(),false));
        Item("强制重算所选大小",async (_,_)=>await Calculate(SelectedRows(),true)); menu.Items.Add(new ToolStripSeparator());
        Item("修改指向…",(_,_)=>Retarget()); Item("删除链接本身…",(_,_)=>DeleteLinks());
        menu.Opening += (_,e)=> { if(busy || SelectedRows().Count==0)e.Cancel=true; };
        grid.ContextMenuStrip=menu;
        grid.KeyDown += (_,e)=> { if(e.Control && e.KeyCode==Keys.C) { Copy(false); e.Handled=true; } if(e.KeyCode==Keys.F5 && !busy) { _=RefreshLinks(); e.Handled=true; } };
    }
    void ConfigureGridColors()
    {
        grid.BackgroundColor=Dark.Surface; grid.GridColor=Dark.Raised;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Dark.Raised; grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=Dark.Text;
        grid.DefaultCellStyle.BackColor=Dark.Surface; grid.DefaultCellStyle.ForeColor=Dark.Text;
        grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(43,80,69); grid.DefaultCellStyle.SelectionForeColor=Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(27,30,35);
        grid.ColumnHeadersDefaultCellStyle.BackColor=Dark.Raised; grid.ColumnHeadersDefaultCellStyle.ForeColor=Dark.Text;
    }
    void DetectInstances()
    {
        string selected=(instances.SelectedItem as EverythingInstance)?.ClassName ?? settings.Instance;
        var found=EverythingIpc.Instances(); instances.Items.Clear(); foreach(var i in found)instances.Items.Add(i);
        int index=found.FindIndex(x=>x.ClassName==selected);
        if(index<0)index=found.FindIndex(x=>x.ClassName.Contains("1.5"));
        if(found.Count>0)instances.SelectedIndex=Math.Max(0,index);
    }
    void SetBusy(bool value)
    { busy=value; foreach(var b in actions)b.Enabled=!value; instances.Enabled=!value; stop.Enabled=value; }
    async Task RefreshLinks()
    {
        if(busy)return; DetectInstances();
        if(instances.SelectedItem is not EverythingInstance instance) { Dark.ShowError(this,"未发现 Everything 客户端。请先启动你已有的 Everything 副本。");return; }
        work=new CancellationTokenSource(); SetBusy(true); var token=work.Token;
        try
        {
            status.Text="正在查询 Everything 已有索引…";
            reply=new(TaskCreationOptions.RunContinuationsAsynchronously); expectedSender=instance.Handle; queryId++;
            var current=reply;
            // Run SendMessageTimeout off the UI thread so the receiving window keeps pumping messages.
            IntPtr replyHwnd=Handle; uint requestId=queryId;
            var send=Task.Run(()=>EverythingIpc.Send(instance,replyHwnd,requestId),token);
            await send;
            byte[] packet=await current.Task.WaitAsync(TimeSpan.FromSeconds(30),token);
            var paths=await Task.Run(()=>EverythingIpc.Parse(packet),token);
            status.Text=$"索引返回 {paths.Count:N0} 个候选目录，正在读取实际链接类型…";
            int ignored=0;
            var found=await Task.Run(()=>
            {
                var list=new List<LinkRow>();
                foreach(string p in paths)
                {
                    token.ThrowIfCancellationRequested();
                    if(!Paths.IsLocalDrive(p)) {ignored++;continue;}
                    // Exclude Dropbox-owned source trees, not links elsewhere pointing into Dropbox.
                    if(p.Split('\\', StringSplitOptions.RemoveEmptyEntries).Any(part =>
                        part.Equals("Dropbox", StringComparison.OrdinalIgnoreCase) ||
                        part.StartsWith("Dropbox (", StringComparison.OrdinalIgnoreCase))) {ignored++;continue;}
                    try
                    {
                        if(new DriveInfo(System.IO.Path.GetPathRoot(p)!).DriveType==DriveType.Network) {ignored++;continue;}
                        var r=Reparse.Read(p); if(cache.TryGetValue(r.Target,out var size))r.Size=size; list.Add(r);
                    }
                    catch(NotSupportedException){ignored++;}
                    catch(Exception e) when(e is IOException or UnauthorizedAccessException or Win32Exception)
                    { list.Add(new LinkRow { Path=p,Error="读取失败："+e.Message }); }
                }
                return list;
            },token);
            rows.Clear(); rows.AddRange(found); Render();
            status.Text=$"发现 {rows.Count:N0} 个目录链接；排除其他重解析点 / 网络索引目录 {ignored:N0} 个。索引覆盖范围由所选 Everything 实例决定。";
        }
        catch(OperationCanceledException){status.Text="已停止，保留原列表。";}
        catch(TimeoutException){Dark.ShowError(this,"Everything 查询超时。确认所选实例已完成索引加载后重试。");status.Text="查询超时";}
        catch(Exception e){Dark.ShowError(this,e.Message);status.Text="查询未完成";}
        finally { reply=null;SetBusy(false);work.Dispose();work=null; }
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x4A && reply!=null && m.WParam==expectedSender)
        {
            var cds=Marshal.PtrToStructure<EverythingIpc.CopyData>(m.LParam);
            if(cds.Id.ToInt64()==queryId && cds.Length>=28 && cds.Length<=64*1024*1024)
            { var bytes=new byte[cds.Length];Marshal.Copy(cds.Data,bytes,0,bytes.Length);reply.TrySetResult(bytes);m.Result=new IntPtr(1);return; }
        }
        base.WndProc(ref m);
    }
    List<LinkRow> SelectedRows()=>grid.SelectedRows.Cast<DataGridViewRow>().Where(r=>r.Tag is LinkRow).Select(r=>(LinkRow)r.Tag!).ToList();
    List<LinkRow> VisibleRows()=>rows.Where(r=>(!hideSystem.Checked||!r.IsSystem) && (!onlyC.Checked||r.Offloaded) &&
        (search.Text.Length==0 || (r.Path+"\n"+r.Target).Contains(search.Text,StringComparison.OrdinalIgnoreCase)) &&
        (state.SelectedIndex==0 || state.SelectedIndex==1 && r.Size==null && r.Error.Length==0 || state.SelectedIndex==2 && r.Size?.Complete==true || state.SelectedIndex==3 && (r.Error.Length>0 || r.Size is {Complete:false}))).ToList();
    void Render()
    {
        var selected=SelectedRows().Select(r=>r.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var list=VisibleRows();
        // Keep unmeasured sizes last in either direction; sort sizes numerically.
        IEnumerable<LinkRow> sorted = sort == "Bytes"
            ? (ascending ? list.OrderBy(r=>r.Size==null).ThenBy(r=>r.Size?.Bytes??0).ThenBy(r=>r.Path, NaturalTextComparer.Instance)
                         : list.OrderBy(r=>r.Size==null).ThenByDescending(r=>r.Size?.Bytes??0).ThenBy(r=>r.Path, NaturalTextComparer.Instance))
            : ascending ? list.OrderBy(SortText, NaturalTextComparer.Instance).ThenBy(r=>r.Path, NaturalTextComparer.Instance)
                        : list.OrderByDescending(SortText, NaturalTextComparer.Instance).ThenBy(r=>r.Path, NaturalTextComparer.Instance);
        // Avoid intermediate repaints and repeated SelectionChanged work while
        // rebuilding the visible rows. One invalidate paints the final state.
        grid.SuspendLayout(); rendering=true;
        bool redrawSuspended=grid.IsHandleCreated;
        if(redrawSuspended) SendMessageW(grid.Handle,0x000B,IntPtr.Zero,IntPtr.Zero);
        try
        {
            grid.Rows.Clear();
            foreach(var r in sorted)
            {
                int i=grid.Rows.Add(r.Name,r.Path,r.Kind,r.Target,r.Size==null?"—":Paths.Format(r.Size.Bytes)+(r.Size.Complete?"":" *"),r.Status+(r.IsSystem?" · 系统属性":""));
                grid.Rows[i].Tag=r;grid.Rows[i].Selected=selected.Contains(r.Path);
                foreach(DataGridViewCell cell in grid.Rows[i].Cells)cell.ToolTipText=cell.Value?.ToString();
            }
            foreach(DataGridViewColumn c in grid.Columns)c.HeaderCell.SortGlyphDirection=c.Name==sort?(ascending?SortOrder.Ascending:SortOrder.Descending):SortOrder.None;
        }
        finally
        {
            if(redrawSuspended) SendMessageW(grid.Handle,0x000B,new IntPtr(1),IntPtr.Zero);
            rendering=false;grid.ResumeLayout(false);grid.Invalidate();
        }
        UpdateTotals();ShowDetail();
    }
    string SortText(LinkRow r)=>sort switch {"Name"=>r.Name,"Kind"=>r.Kind,"Target"=>r.Target,"Status"=>r.Status,_=>r.Path};
    void UpdateTotals()
    {
        var eligible=rows.Where(r=>r.Offloaded && !r.IsSystem).ToList();
        var roots=Paths.UniqueRoots(eligible); long size=roots.Sum(r=>r.Size?.Bytes??0);
        int pending=roots.Count(r=>r.Size==null),partial=roots.Count(r=>r.Size is {Complete:false});
        total.Text=$"C 盘已转出数据  {Paths.Format(size)}"+(pending>0||partial>0?"  · 待完整统计":"");
        summary.Text=$"显示 {grid.Rows.Count:N0} / 共 {rows.Count:N0} 个链接    C 盘转出 {eligible.Count:N0} 项 → 去重 {roots.Count:N0} 个目标    未计算 {pending} · 部分/异常 {partial}    按当前逻辑大小估算";
    }
    void ShowDetail()
    {
        var selected=SelectedRows();if(selected.Count!=1){detail.Text=selected.Count>1?$"已选择 {selected.Count} 项":"选择一项查看完整路径和统计明细；双击打开目标，右键管理。";return;}
        var r=selected[0];var s=r.Size;
        detail.Text=$"链接：{r.Path}\r\n目标：{r.Target}\r\n类型：{r.Kind}{(r.Relative?"（相对目标："+r.StoredTarget+"）":"")}    状态：{r.Status}\r\n"+
            (s==null?"大小尚未计算。":$"逻辑大小：{Paths.Format(s.Bytes)}（{s.Bytes:N0} 字节） · 文件 {s.Files:N0} · 读取失败 {s.Errors} · 跳过内部重解析点 {s.Skipped}  {s.Detail}");
    }
    async Task Calculate(List<LinkRow> targets,bool force)
    {
        if(busy)return;targets=targets.Where(r=>r.Error.Length==0 && r.Kind!="卷挂载点").ToList();
        if(targets.Count==0)return;work=new();var token=work.Token;SetBusy(true);
        try
        {
            var groups=targets.GroupBy(r=>r.Target,StringComparer.OrdinalIgnoreCase).ToList();int done=0;
            foreach(var group in groups)
            {
                token.ThrowIfCancellationRequested();string target=group.Key;SizeResult result;
                if(!force && cache.TryGetValue(target,out var cached))result=cached;
                else
                {
                    status.Text=$"计算 {done+1}/{groups.Count}：{target}";
                    var progress=new Progress<(long bytes,long files)>(p=>status.Text=$"计算 {done+1}/{groups.Count} · {Paths.Format(p.bytes)} · {p.files:N0} 文件：{target}");
                    result=await Task.Run(()=>SizeScanner.Scan(target,token,(b,f)=>((IProgress<(long,long)>)progress).Report((b,f))),token);
                    cache[target]=result;
                }
                foreach(var r in rows.Where(r=>r.Target.Equals(target,StringComparison.OrdinalIgnoreCase)))r.Size=result;
                done++;Render();
            }
            status.Text="大小统计完成。本次缓存可复用；文件变化后请右键“强制重算”。";
        }
        catch(OperationCanceledException){status.Text="已停止计算；已完成的结果保留，当前未完成项目不记入总量。";}
        catch(Exception e){Dark.ShowError(this,e.Message);status.Text="大小统计中断";}
        finally{SetBusy(false);work.Dispose();work=null;UpdateTotals();}
    }
    void OpenTarget(){var r=SelectedRows().FirstOrDefault();if(r==null||r.Target.Length==0)return;Try(()=>Process.Start(new ProcessStartInfo("explorer.exe",Quote(r.Target)){UseShellExecute=true}));}
    void Locate(){var r=SelectedRows().FirstOrDefault();if(r!=null)Try(()=>Process.Start(new ProcessStartInfo("explorer.exe","/select,"+Quote(r.Path)){UseShellExecute=true}));}
    static string Quote(string s)=>"\""+s.TrimEnd('\\')+"\"";
    void Copy(bool target){var list=SelectedRows();if(list.Count>0)Try(()=>Clipboard.SetText(string.Join(Environment.NewLine,list.Select(r=>target?r.Target:r.Path))));}
    void Try(Action a){try{a();}catch(Exception e){Dark.ShowError(this,e.Message);}}
    void CreateLink()
    {
        if(busy)return;using var d=new LinkDialog();if(d.ShowDialog(this)!=DialogResult.OK)return;
        Try(()=>{Reparse.Create(d.LinkPath,d.TargetPath,d.LinkTag);rows.RemoveAll(x=>x.Path.Equals(d.LinkPath,StringComparison.OrdinalIgnoreCase));rows.Add(Reparse.Read(d.LinkPath));Render();status.Text="链接已创建。";});
    }
    void Retarget()
    {
        var list=SelectedRows();if(busy||list.Count!=1)return;var r=list[0];
        if(!r.Editable||r.IsSystem){Dark.ShowError(this,"卷挂载点、系统属性链接或读取失败的项目在首版中只读。");return;}
        using var d=new LinkDialog(r);if(d.ShowDialog(this)!=DialogResult.OK)return;
        if(Dark.Message(this,"确认修改指向",$"链接：{r.Path}\r\n\r\n原目标：{r.Target}\r\n新目标：{d.TargetPath}\r\n\r\n不移动文件。请先退出正在使用该链接的软件。",true)!=DialogResult.OK)return;
        Try(()=>{Reparse.Retarget(r,d.TargetPath);cache.Remove(r.Target);int i=rows.IndexOf(r);rows[i]=Reparse.Read(r.Path);Render();status.Text="链接指向已修改，旧关系已记录。";});
    }
    void DeleteLinks()
    {
        var list=SelectedRows();if(busy||list.Count==0)return;
        if(list.Any(r=>!r.Editable||r.IsSystem)){Dark.ShowError(this,"所选项目包含只读项，请取消选择卷挂载点、系统属性链接或读取失败的项目。");return;}
        string names=string.Join("\r\n",list.Take(12).Select(r=>r.Path));
        if(Dark.Message(this,"确认删除链接本身",$"删除 {list.Count} 个链接，保留目标目录与其中的文件。\r\n\r\n{names}{(list.Count>12?"\r\n…":"")}\r\n\r\n链接关系会写入操作日志。",true)!=DialogResult.OK)return;
        int done=0;Try(()=>{foreach(var r in list){Reparse.Delete(r);rows.Remove(r);done++;}});Render();status.Text=$"已删除 {done}/{list.Count} 个链接；目标文件保留。";
    }
    void Export()
    {
        using var d=new SaveFileDialog{Filter="CSV 清单|*.csv",FileName="LinkShelf_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".csv"};
        if(d.ShowDialog(this)!=DialogResult.OK)return;
        string Escape(string s)=>"\""+s.Replace("\"","\"\"")+"\"";
        // Apostrophe protects arbitrary filenames from spreadsheet formula execution.
        string Safe(string s)=>s.Length>0&&"=+-@\t\r".Contains(s[0])?"'"+s:s;
        Try(()=>{var b=new StringBuilder("名称,链接路径,类型,目标路径,逻辑字节数,文件数,状态,系统属性\r\n");
            foreach(var r in VisibleRows())b.AppendLine(string.Join(",",new[]{r.Name,r.Path,r.Kind,r.Target,r.Size?.Bytes.ToString()??"",r.Size?.Files.ToString()??"",r.Status,r.IsSystem?"是":"否"}.Select(s=>Escape(Safe(s)))));
            File.WriteAllText(d.FileName,b.ToString(),new UTF8Encoding(true));status.Text="当前列表已导出。";});
    }
}
