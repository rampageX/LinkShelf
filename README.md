# LinkShelf

[English](README.en.md) · Windows 目录连接点与目录符号链接管理工具

LinkShelf 是一套轻量、暗色、便携的 Windows 文件夹链接工具：**LinkShelf.exe** 负责集中查看和管理链接，**LinkShelfMover.exe** 负责将普通文件夹安全迁往其他本地磁盘，并在原位置建立链接。主程序直接使用**已经运行的 Everything 实例**查询索引，不重扫所有硬盘、不创建第二套索引；Mover 独立运行，无需 Everything。

**当前稳定版：v1.0.8**。管理器和 Mover 均支持**中英文界面**：根据 Windows 界面语言自动选择中文（`zh-*`）或英文（其他语言）；所有翻译放在同一个便携 `languages.json` 文件中。永久大小缓存、新链接置顶、失效目标单独记录和两种文件夹右键入口继续保留。

## 功能

- 发现并区分目录连接点 `/J`、目录符号链接 `/D`，排除文件链接和其他重解析点。
- 选择 Everything 实例；搜索、筛选、自然排序、定位链接、打开目标、复制路径、导出 CSV。
- 新建链接、修改指向、删除**链接本身**，修改前验证链接状态并记录操作日志。
- 按需后台计算目标目录的逻辑大小，跳过内部重解析点，支持停止、缓存及强制重算。
- 统计 C 盘链接指向其他本地盘的当前逻辑数据量，对相同、嵌套、已解析别名的目标去重。
- 暗色界面、自适应列宽、窗口与列宽记忆、双缓冲防闪烁。
- 界面、右键菜单、弹窗、提示及 Mover 迁移日志中英文双语；无需手工切换或联网下载语言包。
- 目标文件夹大小跨启动缓存，统计行末尾显示创建时间、可点击的“更新”及最近更新时间；新增链接在其分组内继续遵守当前列排序。
- 已扫描到的失效目标在下方明细框顶部逐条列出链接路径与目标路径；目标大小显示“—”，不会算作部分扫描，也不会遮蔽其他正常目标的转出数据。
- Everything 查询左下角按 IPC 连接、等待返回、结果解析分阶段显示；结果等待超时独立提示。
- 左下角最右端「设置」可调整 Everything 查询结果等待超时（默认 30 秒、5–600 秒），保存于 `LinkShelf.settings.json`；IPC 发送超时仍为 5 秒。
- 单行响应式工具栏、自绘暗色悬停提示和深色下拉列表。

### LinkShelf Mover：右键一键搬家

- 运行 `LinkShelfMover.exe`，可选择 **添加一级右键菜单**（推荐）或 **加入「发送到」菜单**。两者独立安装、独立移除，均仅限当前 Windows 用户；不安装 Shell 扩展 DLL、不驻留后台。
- 一级菜单使用当前用户的注册表静态命令，可直接在经典右键菜单选择 **使用 LinkShelf Mover 迁移**；Windows 11 可能需要点击“显示更多选项”。「发送到」则只创建用户级快捷方式、不写注册表。
- WizTree 的「发送到」若额外弹出资源管理器，可以尝试一级菜单入口；此兼容性差异仍需 Windows 本机测试确认。
- 例如右键 `C:\A`，在界面中选择目的地父目录 `D:\B`，自动生成可编辑的完整目标路径 `D:\B\A`。
- 选择**目录连接点 `/J`（默认）**或**目录符号链接 `/D`**，预检通过后先复制、核验，再切换原目录为链接；遇到访问被拒绝可提示 UAC 提权重新打开，保留已填路径及链接类型，**不会未经确认自动执行**。
- 支持进度与取消、迁移日志、异常恢复提示，记忆窗口位置。详情请读 [Mover 使用说明](Tools/LinkShelfMover/README.md)。

## 下载与运行

从 [v1.0.8 Release](https://github.com/rampageX/LinkShelf/releases/tag/v1.0.8) 下载 `LinkShelf_v1.0.8_Windows_x64.zip` 并**完整解压**。运行 `LinkShelf.exe` 管理链接，或运行同目录的 `LinkShelfMover.exe` 迁移普通文件夹。

要求：

1. Windows 10/11 x64。
2. [Microsoft .NET 8 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/8.0)。
3. 使用 LinkShelf 管理器时，需要正在运行、已完成索引的 [Everything](https://www.voidtools.com/) 客户端；单独使用 Mover 时不需要。

LinkShelf 不包含 Everything，不修改其索引。发现范围由你选择的 Everything 实例决定。

便携数据保存在程序目录：`LinkShelf.sizes.json` 永久保存上次统计的逻辑大小和创建/更新时间；`LinkShelf.links.json` 按 Everything 实例保存上次成功发现的链接路径，首次启动只建立基线，不将旧链接误标为新增。这两个文件不会随发布包附带。

语言文件 `languages.json` **随发布包附带**，中英文词条都在其中。程序启动时自动读取 Windows 当前界面语言：中文系统显示中文，其余系统显示英文；重新启动后生效。可以编辑对应 `en` 文本自行调整翻译，格式损坏或文件缺失时会安全回退到程序内置词条。旧版设置、大小缓存及链接快照保持兼容，不用重扫。

“连接 / 刷新”只重新读取 Everything 的链接列表，不重新计算已有文件夹大小；目录内容改变后，点击第二行末尾、创建时间后的“更新”文字链接，可强制重算当前实例发现的本地非系统目标。其后显示最近成功更新时间。

## 安全边界

- 默认隐藏带系统属性的链接；带系统属性的链接和卷挂载点只读。
- 默认不扫描网络目标的大小，也不统计网络目标的 C 盘转出量。
- 删除操作只删除链接对象，**不会递归删除目标文件夹及文件**。
- 管理器的“修改指向”不会移动文件；所有链接写操作前均记录 `LinkShelf.operations.jsonl`。
- 普通文件夹迁移由独立的 `LinkShelfMover.exe` 执行，**会真实移动数据**。它只接受本地 NTFS 普通文件夹，拒绝危险目录、内部重解析点、加密及硬链接等不安全情况。跨盘操作不可能做到真正的原子事务；重要目录请先备份并退出相关软件。
- “C 盘已转出数据”是当前逻辑文件大小的估算，不代表历史释放的磁盘空间或实际物理占用；待计算项和已计算但不完整的部分/异常项会分别标识，避免长期显示“待完整统计”。
- “目标不存在”只表示当前扫描无法找到目标，属于需要检查的失效链接；它仍保留在列表和下方明细中，但不计入转出总量，也不增加部分/异常计数。尚未扫描或访问被拒绝的目标仍会正常提示。

涉及重要目录时，请先退出占用该目录的软件，并确认目标和备份。

## 构建

使用 .NET 8 SDK，在项目根目录执行：

```powershell
dotnet publish Source/LinkShelf.csproj -c Release -r win-x64 --self-contained false -o Build
dotnet publish Tools/LinkShelfMover/LinkShelfMover.csproj -c Release -r win-x64 --self-contained false -o Build/Mover
```

主程序源码在 `Source/`，Mover 源码在 `Tools/LinkShelfMover/`。完整运行包包含源码和两套 Windows 原生测试：`Tests/Run_Tests.cmd` 与 `Tests/Run_Mover_Tests.cmd`，仅操作临时测试目录。GitHub Actions 对两个 EXE 分别构建、测试后才发布。

## 版本

当前稳定版：**v1.0.8**。详细变更见 [CHANGELOG.md](CHANGELOG.md)。

## 许可证

[MIT](LICENSE)。应用图标为 Windows Forms 标准 `wfc` 图标，来源与上游许可记录见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
