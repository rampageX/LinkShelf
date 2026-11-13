using Microsoft.Win32;
using LinkShelf.Core;

namespace LinkShelf.Mover;

/// <summary>
/// An optional, per-user static shell verb for folder objects. No COM shell
/// extension, background service, admin rights, or machine-wide registry keys.
/// </summary>
public static class ContextMenuInstaller
{
    private const string MenuKey = @"Software\Classes\Directory\shell\LinkShelf.Mover";
    private const string OwnerValue = "LinkShelfOwner";
    private const string OwnerMarker = "LinkShelfMover.DirectoryVerb.v1";
    private const string Caption = "使用 LinkShelf Mover 迁移";

    public static bool Installed => IsInstalledAt(MenuKey);

    public static void Install()
    {
        string executable = Environment.ProcessPath ??
            throw new IOException(L.T("无法定位当前程序的 exe 文件。"));
        InstallAt(MenuKey, executable);
    }

    public static void Uninstall() => UninstallAt(MenuKey);

    internal static bool IsInstalledAt(string keyPath)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath);
        return string.Equals(key?.GetValue(OwnerValue) as string, OwnerMarker, StringComparison.Ordinal);
    }

    internal static string BuildCommand(string executable) => $"\"{executable}\" \"%1\"";

    internal static void InstallAt(string keyPath, string executable)
    {
        if (string.IsNullOrWhiteSpace(executable) || executable.Contains('"'))
            throw new ArgumentException(L.T("程序路径无效。"), nameof(executable));

        using (RegistryKey? existing = Registry.CurrentUser.OpenSubKey(keyPath))
        {
            if (existing != null)
                throw new IOException(IsInstalledAt(keyPath)
                    ? L.T("一级右键菜单已安装；如需更换程序位置，请先移除，再从新位置重新安装。")
                    : L.T("检测到同名注册表菜单项，但它不属于 LinkShelf，拒绝覆盖。"));
        }

        bool created = false;
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true) ??
                throw new IOException(L.T("无法创建当前用户的右键菜单注册表项。"));
            created = true;
            key.SetValue("MUIVerb", L.T(Caption), RegistryValueKind.String);
            // Use a plain executable path: some third-party shell menus do not resolve ",0".
            key.SetValue("Icon", executable, RegistryValueKind.String);
            key.SetValue("MultiSelectModel", "Single", RegistryValueKind.String);
            key.SetValue("Position", "Top", RegistryValueKind.String);
            using (RegistryKey command = key.CreateSubKey("command", writable: true) ??
                throw new IOException(L.T("无法创建右键菜单启动命令。")))
                command.SetValue(null, BuildCommand(executable), RegistryValueKind.String);
            // Write ownership last. A partial installation must not be treated as ours.
            key.SetValue(OwnerValue, OwnerMarker, RegistryValueKind.String);
        }
        catch
        {
            // Only delete our freshly-created, reserved key; never touch a
            // pre-existing entry belonging to the user or another program.
            if (created)
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false); }
                catch { /* preserve the original registration error */ }
            }
            throw;
        }
    }

    internal static void UninstallAt(string keyPath)
    {
        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath))
        {
            if (key == null) return;
            if (!IsInstalledAt(keyPath))
                throw new IOException(L.T("同名菜单项不属于 LinkShelf Mover，拒绝删除。"));
            // Refuse to remove an entry subsequently extended by anyone else.
            if (key.GetSubKeyNames().Any(name => !name.Equals("command", StringComparison.OrdinalIgnoreCase)) ||
                key.GetValueNames().Any(name => name is not ("MUIVerb" or "Icon" or "MultiSelectModel" or "Position" or OwnerValue)))
                throw new IOException(L.T("菜单项包含额外配置，拒绝自动删除，请人工检查。"));
            using RegistryKey? command = key.OpenSubKey("command");
            if (command == null || command.SubKeyCount != 0 || command.GetValueNames().Any(name => name != ""))
                throw new IOException(L.T("菜单启动命令结构已改变，拒绝自动删除。"));
        }
        Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
    }
}
