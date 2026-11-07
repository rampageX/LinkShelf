# LinkShelf

[简体中文](README.md)

LinkShelf is a portable dark-themed folder-link manager for Windows. It uses the index of an **already running Everything instance** over IPC—no full-disk rescan or separate index.

### Highlights

- Discover directory junctions (`/J`) and directory symbolic links (`/D`), excluding file links and unrelated reparse points.
- Search, filter, sort, locate, open, copy paths, and export CSV.
- Create, retarget, and delete the **link itself** with pre-operation logging and stale-link checks.
- Calculate logical target folder sizes on demand, skipping nested reparse points, with cancellation and session caching.
- Estimate logical data offloaded from `C:` to other local drives while deduplicating identical, nested, and resolved alias targets.
- Dark UI, remembered layout, adaptive columns, and reduced repainting.

### Requirements

Windows 10/11 x64, [.NET 8 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/8.0), and a running [Everything](https://www.voidtools.com/) client with an existing index.

Download the portable ZIP from [Releases](https://github.com/rampageX/LinkShelf/releases), extract it, and run `LinkShelf.exe`.

### Important limitations

- The first stable release **does not migrate ordinary folders**.
- System-attribute links and volume mount points are read-only.
- Network targets are not included in size scans or offload totals.
- Size totals are estimates of current *logical* data, not historical freed space or physical disk usage.
- Deleting a link is designed to preserve its target data. Close applications using a link before editing it, and keep backups of important data.

### Build

```powershell
dotnet publish Source/LinkShelf.csproj -c Release -r win-x64 --self-contained false -o Build
```

### License

[MIT](LICENSE). The standard Windows Forms icon is credited in [ICON_SOURCE.txt](Source/Assets/ICON_SOURCE.txt).
