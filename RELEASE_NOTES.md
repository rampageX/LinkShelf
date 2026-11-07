# LinkShelf v1.0.2 — 文件夹一级右键菜单

本版继续统一发布 `LinkShelf.exe` 与 `LinkShelfMover.exe`，新增一个更直接、更清爽的 Mover 启动方式。

**新功能**

- Mover 增加 **“添加一级右键菜单” / “移除一级右键菜单”** 按钮。安装后，右键普通文件夹可选择 **“使用 LinkShelf Mover 迁移”**，无需展开「发送到」。Windows 11 新式右键菜单中可能需要先选择 **“显示更多选项”**。
- 原有「发送到」菜单完整保留，两个入口分别安装、分别卸载；可以在安装一级菜单后删除原「发送到」入口。
- 一级菜单只写入当前用户的 `HKCU\Software\Classes\Directory\shell\LinkShelf.Mover` 静态命令，不需要管理员权限；不安装 Shell 扩展 DLL、后台服务或系统级组件。
- 注册时拒绝覆盖非 LinkShelf 项目；卸载时核验专用所有权标记和命令结构，避免误删用户其它右键菜单。移动程序目录后，请先卸载旧入口，再从新目录重新安装。
- WizTree 4.33 从「发送到」启动 Mover 时，部分环境可能额外打开源目录的资源管理器窗口。新增一级入口用于避开该子菜单，但**是否彻底解决需要用户 Windows 本机确认**。

**保留功能**

- LinkShelf 主程序通过 Everything 索引管理 NTFS `/J` 目录连接点和 `/D` 符号链接，支持大小永久缓存、新链接置顶、C 盘转出数据统计。
- Mover 支持本地 NTFS 普通文件夹搬迁、`/J` 或 `/D` 可选、SHA-256 文件校验、危险目录/权限/占用检查、原目录备份、失败回退、UAC 提权重开与明确的迁移完成日志。
- Windows 10/11 x64、.NET 8 Desktop Runtime x64；Mover 无需 Everything，管理器需要已运行的 Everything 实例。

下载 `LinkShelf_v1.0.2_Windows_x64.zip` 并完整解压。两个 EXE、完整源码、中文/英文说明及 Windows 原生测试均包含在包中。迁移重要目录前请关闭占用程序并做好备份。

GitHub Actions 会在 Windows x64 上构建两款程序并运行两套原生测试后才发布。一级右键在 WizTree 的实际表现尚未实机验收。
