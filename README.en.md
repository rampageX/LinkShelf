# LinkShelf

[简体中文](README.md)

LinkShelf is a portable, dark-themed Windows folder-link toolkit with two executables: **LinkShelf.exe** manages existing links using an **already running Everything instance** over IPC (no full-disk rescan), while **LinkShelfMover.exe** migrates ordinary folders and creates links at their original locations without requiring Everything.

**Current stable release: v1.0.4**. Both the manager and folder mover ship together. Missing targets are reported in the detail pane, but excluded from offload totals and incomplete/error counts. Mover retains its optional per-user direct context-menu entry and SendTo shortcut.

### Highlights

- Discover directory junctions (`/J`) and directory symbolic links (`/D`), excluding file links and unrelated reparse points.
- Search, filter, sort, locate, open, copy paths, and export CSV.
- Create, retarget, and delete the **link itself** with pre-operation logging and stale-link checks.
- Calculate logical target folder sizes on demand, skipping nested reparse points, with cancellation and persistent caching.
- Estimate logical data offloaded from `C:` to other local drives while deduplicating identical, nested, and resolved alias targets.
- Missing targets are listed with both link and target paths at the top of the detail pane. They do not contribute bytes or incomplete-scan counts, and cannot shadow valid targets during deduplication.
- Dark UI, remembered layout, adaptive columns, and reduced repainting.
- Responsive single-row toolbar, dark custom tooltips, per-Everything-instance link snapshots, and a text link between the cache creation and last-update timestamps for refreshing cached sizes.
- **Mover:** Install either **添加一级右键菜单** (Add direct folder menu) or the existing SendTo shortcut. The direct action is **使用 LinkShelf Mover 迁移** (Move with LinkShelf Mover), shown in the classic context menu; Windows 11 may require **Show more options**. Both entries can be removed independently. Select the destination parent directory, then edit the generated full path if desired. Choose a junction (`/J`, default) or directory symlink (`/D`).
- **Mover safety:** Validates source, permissions, free space, and destination; copies and hashes data before replacing the original path with a verified link. Requests UAC elevation on access-denied errors, preserving the requested paths and type for explicit reconfirmation. No shell extensions or services: the direct menu uses a removable static verb under the current user's registry, while SendTo uses only a shortcut.

### Requirements

Windows 10/11 x64 and [.NET 8 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/8.0). The manager additionally requires a running [Everything](https://www.voidtools.com/) client with an existing index; Mover does not.

Download `LinkShelf_v1.0.4_Windows_x64.zip` from the [v1.0.4 Release](https://github.com/rampageX/LinkShelf/releases/tag/v1.0.4), extract the entire ZIP, and run either `LinkShelf.exe` or `LinkShelfMover.exe`.

Portable state stays beside the executable: `LinkShelf.sizes.json` holds cached logical sizes and timestamps; `LinkShelf.links.json` holds link snapshots per Everything instance. No existing links are marked as new during first-run baseline creation. **连接 / 刷新** (Connect / Refresh) only refreshes the indexed link list; the **更新** (Update) text link on the second statistics line recalculates folder sizes. The heading separately flags pending scans and incomplete/error scans; cached sizes are not live measurements.

### Important limitations

- Folder migration is supported **only through LinkShelfMover.exe**, not through the manager's retarget command. Mover operates only on local NTFS directories and deliberately rejects potentially unsafe file types or directory structures.
- Cross-volume migration is **not atomic**. Even with prechecks and rollback, unexpected process activity, file locks, or power failures can interrupt the operation. Close relevant apps and back up important folders first.
- System-attribute links and volume mount points are read-only.
- Network targets are not included in size scans or offload totals.
- Size totals are estimates of current *logical* data, not historical freed space or physical disk usage.
- Only confirmed missing targets are exempt from totals/incomplete counts; unmeasured or inaccessible targets remain visibly flagged. Missing-link detection reflects the most recent size scan, not a live filesystem watch.
- Deleting a link is designed to preserve its target data. Close applications using a link before editing it, and keep backups of important data.

### Build

```powershell
dotnet publish Source/LinkShelf.csproj -c Release -r win-x64 --self-contained false -o Build
dotnet publish Tools/LinkShelfMover/LinkShelfMover.csproj -c Release -r win-x64 --self-contained false -o Build/Mover
```

The release ZIP includes source for both tools and native Windows regression tests. For the detailed Mover workflow and limitations, see [Mover documentation](Tools/LinkShelfMover/README.md).

### License

[MIT](LICENSE). The standard Windows Forms icon is credited in [ICON_SOURCE.txt](Source/Assets/ICON_SOURCE.txt).
