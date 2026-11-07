# LinkShelf

[English](README.en.md) · Windows 目录连接点与目录符号链接管理工具

LinkShelf 是一款轻量、暗色、便携的 Windows 文件夹链接管理器。它直接使用**已经运行的 Everything 实例**查询索引中的目录链接，不重扫所有硬盘、不创建第二套索引。适合把占用系统盘的应用目录迁往其他本地磁盘后，集中查看和管理这些链接。

**当前稳定版：v0.1.6**。新增永久文件夹大小缓存、按 Everything 实例记忆链接列表；新发现的链接绿色高亮并始终置顶，与原有链接之间使用无文字的细暗色分隔条。

## 功能

- 发现并区分目录连接点 `/J`、目录符号链接 `/D`，排除文件链接和其他重解析点。
- 选择 Everything 实例；搜索、筛选、自然排序、定位链接、打开目标、复制路径、导出 CSV。
- 新建链接、修改指向、删除**链接本身**，修改前验证链接状态并记录操作日志。
- 按需后台计算目标目录的逻辑大小，跳过内部重解析点，支持停止、缓存及强制重算。
- 统计 C 盘链接指向其他本地盘的当前逻辑数据量，对相同、嵌套、已解析别名的目标去重。
- 暗色界面、自适应列宽、窗口与列宽记忆、双缓冲防闪烁。
- 目标文件夹大小跨启动缓存，统计信息末尾显示缓存创建时间及“更新”文字链接；新增链接在其分组内继续遵守当前列排序。
- 单行响应式工具栏、自绘暗色悬停提示和深色下拉列表。

## 下载与运行

从 [v0.1.6 Release](https://github.com/rampageX/LinkShelf/releases/tag/v0.1.6) 下载 `LinkShelf_v0.1.6_Windows_x64.zip`，解压后运行 `LinkShelf.exe`。

要求：

1. Windows 10/11 x64。
2. [Microsoft .NET 8 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/8.0)。
3. 正在运行、已完成索引的 [Everything](https://www.voidtools.com/) 客户端。

LinkShelf 不包含 Everything，不修改其索引。发现范围由你选择的 Everything 实例决定。

便携数据保存在程序目录：`LinkShelf.sizes.json` 永久保存上次统计的逻辑大小和创建/更新时间；`LinkShelf.links.json` 按 Everything 实例保存上次成功发现的链接路径，首次启动只建立基线，不将旧链接误标为新增。这两个文件不会随发布包附带。

“连接 / 刷新”只重新读取 Everything 的链接列表，不重新计算已有文件夹大小；目录内容改变后，点击第二行末尾“缓存大小”后的“更新”文字链接，可强制重算当前实例发现的本地非系统目标。

## 安全边界

- 默认隐藏带系统属性的链接；带系统属性的链接和卷挂载点只读。
- 默认不扫描网络目标的大小，也不统计网络目标的 C 盘转出量。
- 删除操作只删除链接对象，**不会递归删除目标文件夹及文件**。
- 修改指向不会移动文件；所有写操作前均记录 `LinkShelf.operations.jsonl`。
- 首个正式版本**不提供普通文件夹迁移**。请自行完成数据迁移后再创建链接。
- “C 盘已转出数据”是当前逻辑文件大小的估算，不代表历史释放的磁盘空间或实际物理占用；未计算和部分结果会明确标识。

涉及重要目录时，请先退出占用该目录的软件，并确认目标和备份。

## 构建

使用 .NET 8 SDK，在项目根目录执行：

```powershell
dotnet publish Source/LinkShelf.csproj -c Release -r win-x64 --self-contained false -o Build
```

项目源代码位于 `Source/`；测试源代码位于 `Source/Tests/`。完整运行包还提供测试程序，`Tests/Run_Tests.cmd` 在 Windows 上验证临时目录中的原生链接操作。已配置 GitHub Actions 自动构建与发布。

## 版本

当前稳定版：**v0.1.6**（首个稳定版 v0.1.4）。详细变更见 [CHANGELOG.md](CHANGELOG.md)。

## 许可证

[MIT](LICENSE)。应用图标为 Windows Forms 标准 `wfc` 图标，来源与上游许可记录见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
