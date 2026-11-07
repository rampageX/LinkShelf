using LinkShelf.Core;
using LinkShelf.UI;

namespace LinkShelf.Mover;

public sealed class MoverForm : Form
{
    private readonly TextBox sourceBox = new() { AutoSize = false, Height = 27,
        Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 6, 0), ReadOnly = true };
    private readonly TextBox destinationBox = new() { AutoSize = false, Height = 27,
        Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 6, 0) };
    private readonly DarkComboBox linkTypeBox = new() { Height = 27, Width = 220,
        Anchor = AnchorStyles.Left, Margin = new Padding(0) };
    private readonly TextBox logBox = new() { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true,
        ScrollBars = ScrollBars.Vertical, WordWrap = true };
    private readonly Label stateLabel = new() { Dock = DockStyle.Fill,
        Text = "准备就绪。源目录会保留到新目标核验完成之后。", AutoSize = false };
    private readonly DarkButton sendToButton = new() { Height = 34, Width = 170 };
    private readonly DarkButton startButton = new() { Text = "检查并开始迁移", Height = 36, Width = 150 };
    private readonly DarkButton cancelButton = new() { Text = "取消任务", Height = 36, Width = 92, Enabled = false };
    private readonly DarkButton closeButton = new() { Text = "关闭", Height = 36, Width = 85 };
    private readonly DarkButton selectSource = new() { Text = "浏览…", Height = 27,
        Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0) };
    private readonly DarkButton selectTargetParent = new() { Text = "选择父目录…", Height = 27,
        Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0) };
    private CancellationTokenSource? running;
    private bool busy;

    public MoverForm(string source, string destination = "", uint initialLinkTag = Reparse.Mount)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        Text = "LinkShelf Mover · 移动目录并创建链接 · 1.0.0";
        ClientSize = new Size(920, 530);
        MinimumSize = new Size(810, 505);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);

        var grid = new DarkTableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 9,
            Padding = new Padding(20, 16, 20, 14),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        foreach (int h in new[] { 42, 36, 36, 36, 56, 45, 34, 0, 48 })
            grid.RowStyles.Add(h == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, h));

        var title = new Label
        {
            Text = "移动普通文件夹，原位置自动建立目录连接点 /J",
            Font = new Font("Microsoft YaHei UI", 12.5F, FontStyle.Bold),
            ForeColor = Dark.Accent, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        grid.Controls.Add(title, 0, 0); grid.SetColumnSpan(title, 3);

        grid.Controls.Add(NewLabel("原目录"), 0, 1);
        grid.Controls.Add(sourceBox, 1, 1);
        grid.Controls.Add(selectSource, 2, 1);
        grid.Controls.Add(NewLabel("迁往"), 0, 2);
        grid.Controls.Add(destinationBox, 1, 2);
        grid.Controls.Add(selectTargetParent, 2, 2);
        grid.Controls.Add(NewLabel("链接类型"), 0, 3);
        linkTypeBox.Items.AddRange(["目录连接点 /J", "目录符号链接 /D"]);
        linkTypeBox.SelectedIndex = initialLinkTag == Reparse.Symlink ? 1 : 0;
        grid.Controls.Add(linkTypeBox, 1, 3);

        var help = new Label
        {
            Text = "例如：右键 C:\\A → 选择 D:\\B → 自动填写 D:\\B\\A。目标路径可直接修改。\n"
                 + "仅迁移本地 NTFS 普通目录；检查被占用、权限、空间及内部链接。文件复制并核验后才切换原目录。",
            ForeColor = Dark.Muted, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        grid.Controls.Add(help, 0, 4); grid.SetColumnSpan(help, 3);

        var controls = new DarkFlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        sendToButton.Text = SendToInstaller.Installed ? "从‘发送到’菜单移除" : "加入‘发送到’菜单";
        controls.Controls.Add(sendToButton);
        var note = new Label { Text = "只安装当前用户的快捷方式；不写注册表、不驻留后台。",
            AutoSize = true, ForeColor = Dark.Muted, Padding = new Padding(9, 8, 0, 0) };
        controls.Controls.Add(note);
        grid.Controls.Add(controls, 0, 5); grid.SetColumnSpan(controls, 3);
        grid.Controls.Add(stateLabel, 0, 6); grid.SetColumnSpan(stateLabel, 3);
        grid.Controls.Add(logBox, 0, 7); grid.SetColumnSpan(logBox, 3);
        var footer = new DarkFlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill,
            Padding = new Padding(0, 6, 0, 0)
        };
        footer.Controls.Add(closeButton);
        footer.Controls.Add(cancelButton);
        footer.Controls.Add(startButton);
        grid.Controls.Add(footer, 0, 8); grid.SetColumnSpan(footer, 3);
        Controls.Add(grid);
        Dark.Apply(this);
        stateLabel.ForeColor = Dark.Muted;
        help.ForeColor = Dark.Muted;
        title.ForeColor = Dark.Accent;
        note.ForeColor = Dark.Muted;

        sourceBox.Text = source;
        destinationBox.Text = destination;
        void RefreshHeading() => title.Text = linkTypeBox.SelectedIndex == 1
            ? "移动普通文件夹，原位置自动建立目录符号链接 /D"
            : "移动普通文件夹，原位置自动建立目录连接点 /J";
        linkTypeBox.SelectedIndexChanged += (_, _) => RefreshHeading();
        RefreshHeading();
        MoverWindowSettings.Load().Apply(this);
        selectSource.Click += (_, _) => BrowseSource();
        selectTargetParent.Click += (_, _) => BrowseTarget();
        sendToButton.Click += (_, _) => ToggleSendTo();
        startButton.Click += async (_, _) => await StartMoveAsync();
        cancelButton.Click += (_, _) => { running?.Cancel(); cancelButton.Enabled = false; WriteLine("正在终止复制/校验…"); };
        closeButton.Click += (_, _) => Close();
        Shown += (_, _) => CheckInterruptedTasks();
        FormClosing += (_, e) =>
        {
            if (!busy) return;
            e.Cancel = true;
            Dark.Message(this, "任务进行中", "请等待迁移完成；复制/校验期间可使用‘取消任务’。提交链接阶段不支持中断。");
        };
        FormClosed += (_, _) => MoverWindowSettings.Save(this);
    }

    private static Label NewLabel(string text) => new()
    {
        Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight,
        Margin = new Padding(0, 0, 9, 0),
    };

    private void BrowseSource()
    {
        using var dlg = new FolderBrowserDialog { Description = "选择准备迁移的原始普通文件夹",
            UseDescriptionForTitle = true, ShowNewFolderButton = false };
        if (Directory.Exists(sourceBox.Text)) dlg.InitialDirectory = sourceBox.Text;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        sourceBox.Text = dlg.SelectedPath;
    }

    private void BrowseTarget()
    {
        if (string.IsNullOrWhiteSpace(sourceBox.Text) || !Directory.Exists(sourceBox.Text))
        { Dark.ShowError(this, "请先选择一个存在的源文件夹。"); return; }
        using var dlg = new FolderBrowserDialog { Description = "选择目标父目录（例如 D:\\B）",
            UseDescriptionForTitle = true, ShowNewFolderButton = true };
        try
        {
            string? current = Path.GetDirectoryName(destinationBox.Text);
            if (current != null && Directory.Exists(current)) dlg.InitialDirectory = current;
        }
        catch { /* destination can be edited freely */ }
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        destinationBox.Text = Path.Combine(dlg.SelectedPath,
            Path.GetFileName(sourceBox.Text.TrimEnd('\\')));
        destinationBox.SelectionStart = destinationBox.TextLength;
    }

    private void ToggleSendTo()
    {
        try
        {
            if (SendToInstaller.Installed) SendToInstaller.Uninstall();
            else SendToInstaller.Install();
            sendToButton.Text = SendToInstaller.Installed ? "从‘发送到’菜单移除" : "加入‘发送到’菜单";
            WriteLine(SendToInstaller.Installed ? "已加入‘发送到’菜单。" : "已从‘发送到’菜单移除。");
        }
        catch (Exception ex) { Dark.ShowError(this, "操作菜单失败：" + ex.Message); }
    }

    private async Task StartMoveAsync()
    {
        if (busy) return;
        MovePlan plan;
        try { plan = PathRules.MakePlan(sourceBox.Text, destinationBox.Text); }
        catch (Exception e) { Dark.ShowError(this, e.Message); return; }
        uint linkTag = linkTypeBox.SelectedIndex == 1 ? Reparse.Symlink : Reparse.Mount;
        string linkName = linkTag == Reparse.Mount ? "目录连接点 /J" : "目录符号链接 /D";
        if (Dark.Message(this, "确认迁移", $"将进行真实的跨目录迁移：\n\n" +
            $"原目录：{plan.Source}\n目标：{plan.Destination}\n链接类型：{linkName}\n\n" +
            "请先退出使用该目录的软件。\n" +
            "程序先检查/复制/校验，再把原目录改为连接点；只有链接验证成功后才清理旧文件。\n" +
            "无法绝对保证运行中没有其他程序修改文件；请确保重要数据有独立备份。", confirm: true) != DialogResult.OK) return;

        busy = true;
        running = new CancellationTokenSource();
        startButton.Enabled = false; selectSource.Enabled = false; selectTargetParent.Enabled = false;
        sourceBox.Enabled = false; destinationBox.Enabled = false;
        sendToButton.Enabled = false; closeButton.Enabled = false; linkTypeBox.Enabled = false;
        cancelButton.Enabled = true;
        logBox.Clear();
        var progress = new Progress<MoveProgress>(p =>
        {
            stateLabel.Text = p.Message;
            cancelButton.Enabled = p.CanCancel && running is { IsCancellationRequested: false };
            WriteLine(p.Message);
        });
        bool closeAfterElevation = false;
        try
        {
            MoveResult result = await MigrationEngine.MoveAsync(plan.Source, plan.Destination, progress, running.Token, linkTag);
            string message = $"迁移成功：{result.FileCount:N0} 个文件，{Paths.Format(result.Bytes)}。\n" +
                $"原路径：{result.Source}\n目标：{result.Destination}\n类型：{linkName}";
            if (result.BackupLeft != null)
                message += "\n\n注意：连接点已生效，但旧目录备份未删干净，请人工检查：\n" + result.BackupLeft;
            stateLabel.Text = result.BackupLeft == null ? "迁移完成，目录链接正常。" : "目录链接已创建；旧目录备份待清理。";
            Dark.Message(this, "迁移结果", message);
        }
        catch (OperationCanceledException)
        {
            stateLabel.Text = "已取消，原目录未迁移。";
            WriteLine("任务已取消。复制用的临时目录会尽力清理。");
        }
        catch (Exception ex)
        {
            stateLabel.Text = "迁移未完成，请查看错误详情。";
            WriteLine(ex.Message);
            if (Elevation.IsAccessDenied(ex) && !Elevation.IsAdministrator)
            {
                bool retry = Dark.Message(this, "权限不足", ex.Message + "\n\n" +
                    "是否以管理员身份重新打开此工具？\n" +
                    "会保留原目录和目标路径，重新确认后才开始迁移。", confirm: true) == DialogResult.OK;
                if (retry)
                {
                    try
                    {
                        if (Elevation.RestartAsAdministrator(plan.Source, plan.Destination, linkTag))
                        {
                            // The elevated child waits briefly for the parent's
                            // single-instance mutex to be released.
                            closeAfterElevation = true;
                            WriteLine("已请求管理员权限，迁移尚未重新开始。");
                        }
                        else WriteLine("已取消管理员授权；原目录未迁移。");
                    }
                    catch (Exception startError)
                    { Dark.ShowError(this, "无法重新以管理员身份启动：" + startError.Message); }
                }
            }
            else
            {
                Dark.ShowError(this, ex.Message +
                    (Elevation.IsAccessDenied(ex) ? "\n\n当前已经具有管理员权限，请检查目录 ACL、占用程序及防护软件设置。" : ""));
            }
        }
        finally
        {
            busy = false;
            running.Dispose(); running = null;
            startButton.Enabled = selectSource.Enabled = selectTargetParent.Enabled =
                sourceBox.Enabled = destinationBox.Enabled = sendToButton.Enabled = closeButton.Enabled =
                    linkTypeBox.Enabled = true;
            cancelButton.Enabled = false;
            if (closeAfterElevation) BeginInvoke(Close);
        }
    }

    private void WriteLine(string line)
    {
        if (logBox.TextLength > 24_000) logBox.Text = logBox.Text[^12_000..];
        logBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + line + Environment.NewLine);
    }

    private void CheckInterruptedTasks()
    {
        foreach (MoveJournal pending in MoveJournal.Unfinished().ToArray())
        {
            if (!Directory.Exists(pending.Backup)) continue;
            bool originalExists = Directory.Exists(pending.Source);
            if (!originalExists)
            {
                bool restore = Dark.Message(this, "检测到中断的迁移", "发现上次操作留下的源目录备份，原路径目前不存在。\n\n" +
                    "源备份：" + pending.Backup + "\n原路径：" + pending.Source + "\n\n" +
                    "是否立即把备份目录改名恢复到原位置？目标盘上的数据不会被删除。", true) == DialogResult.OK;
                if (!restore) continue;
                try
                {
                    Directory.Move(pending.Backup, pending.Source);
                    pending.SetPhase("ROLLED_BACK");
                    WriteLine("上次中断的目录已恢复：" + pending.Source);
                }
                catch (Exception ex) { Dark.ShowError(this, "恢复失败，请保留备份目录：" + ex.Message); }
            }
            else
            {
                WriteLine("检测到旧目录备份尚在，请核实后手工清理：" + pending.Backup);
                stateLabel.Text = "提示：上次迁移遗留备份，详情见下方记录。";
            }
        }
    }
}
