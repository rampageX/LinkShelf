# LinkShelf

[English](README.en.md) · Windows 目录连接点与目录符号链接管理工具

LinkShelf 是一款轻量、暗色、便携的 Windows 文件夹链接管理器。它直接使用**已经运行的 Everything 实例**查询索引中的目录链接，不重扫所有硬盘、不创建第二套索引。适合把占用系统盘的应用目录迁往其他本地磁盘后，集中查看和管理这些链接。

## 功能

- 发现并区分目录连接点 `/J`、目录符号链接 `/D`，排除文件链接和其他重解析点。
- 选择 Everything 实例；搜索、筛选、自然排序、定位链接、打开目标、复制路径、导出 CSV。
- 新建链接、修改指向、删除**链接本身**，修改前验证链接状态并记录操作日志。
- 按需后台计算目标目录的逻辑大小，跳过内部重解析点，支持停止、缓存及强制重算。
- 统计 C 盘链接指向其他本地盘的当前逻辑数据量，对相同、嵌套、已解析别名的目标去重。
- 暗色界面、自适应列宽、窗口与列宽记忆、双缓冲防闪烁。

## 下载与运行

从 [Releases](https://github.com/rampageX/LinkShelf/releases) 下载 `LinkShelf_v0.1.4_Windows_x64.zip`，解压后运行 `LinkShelf.exe`。

要求：

1. Windows 10/11 x64。
2. [Microsoft .NET 8 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/8.0)。
3. 正在运行、已完成索引的 [Everything](https://www.voidtools.com/) 客户端。

LinkShelf 不包含 Everything，不修改其索引。发现范围由你选择的 Everything 实例决定。

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

首个稳定版：**v0.1.4**。详细变更见 [CHANGELOG.md](CHANGELOG.md)。

## 许可证

[MIT](LICENSE)。应用图标为 Windows Forms 标准 `wfc` 图标，来源与上游许可记录见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
