# LinkShelf

[简体中文](README.md)

LinkShelf is a portable dark-themed folder-link manager for Windows. It uses the index of an **already running Everything instance** over IPC—no full-disk rescan or separate index.

**Current stable release: v0.1.6**. Folder sizes persist across launches, and links discovered since the previous run are highlighted and pinned above existing links, separated by a slim unlabeled bar.

### Highlights

- Discover directory junctions (`/J`) and directory symbolic links (`/D`), excluding file links and unrelated reparse points.
- Search, filter, sort, locate, open, copy paths, and export CSV.
- Create, retarget, and delete the **link itself** with pre-operation logging and stale-link checks.
- Calculate logical target folder sizes on demand, skipping nested reparse points, with cancellation and persistent caching.
- Estimate logical data offloaded from `C:` to other local drives while deduplicating identical, nested, and resolved alias targets.
- Dark UI, remembered layout, adaptive columns, and reduced repainting.
- Responsive single-row toolbar, dark custom tooltips, per-Everything-instance link snapshots, and a text link for refreshing cached sizes.

### Requirements

Windows 10/11 x64, [.NET 8 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/8.0), and a running [Everything](https://www.voidtools.com/) client with an existing index.

Download `LinkShelf_v0.1.6_Windows_x64.zip` from the [v0.1.6 Release](https://github.com/rampageX/LinkShelf/releases/tag/v0.1.6), extract it, and run `LinkShelf.exe`.

Portable state stays beside the executable: `LinkShelf.sizes.json` holds cached logical sizes and timestamps; `LinkShelf.links.json` holds link snapshots per Everything instance. No existing links are marked as new during first-run baseline creation. **连接 / 刷新** (Connect / Refresh) only refreshes the indexed link list; the **更新** (Update) text link on the second statistics line recalculates folder sizes. Cached sizes are not live measurements.

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
