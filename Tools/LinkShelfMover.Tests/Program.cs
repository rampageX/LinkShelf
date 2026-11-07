using LinkShelf.Core;
using LinkShelf.Mover;
using System.ComponentModel;

string scratch = Path.Combine(Path.GetTempPath(), "LinkShelfMover_native_tests_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scratch);
int passed = 0;
void Pass(string name) { passed++; Console.WriteLine("PASS: " + name); }
void Assert(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); Pass(name); }

async Task MustFail(string name, string source, string target, bool targetPreexists = false)
{
    bool failed = false;
    try { await MigrationEngine.MoveAsync(source, target, new Progress<MoveProgress>(), CancellationToken.None); }
    catch (Exception) { failed = true; }
    Assert(failed && Directory.Exists(source) && Directory.Exists(target) == targetPreexists, name);
}

try
{
    string home = Path.Combine(scratch, "case1");
    string src = Path.Combine(home, "源目录 A");
    string parent = Path.Combine(home, "Destination");
    string dst = Path.Combine(parent, "改过名字 A");
    Directory.CreateDirectory(Path.Combine(src, "empty"));
    Directory.CreateDirectory(Path.Combine(src, "nested"));
    Directory.CreateDirectory(parent);
    File.WriteAllText(Path.Combine(src, "a.txt"), "目录连接点测试 🦆");
    File.WriteAllBytes(Path.Combine(src, "nested", "file.bin"), Enumerable.Range(0, 255).Select(x => (byte)x).ToArray());
    var plans = PathRules.MakePlan(src, dst);
    Assert(plans.Destination == dst && plans.Source == src, "source/destination path planning");
    MoveResult done = await MigrationEngine.MoveAsync(src, dst, new Progress<MoveProgress>(), CancellationToken.None);
    Assert(done.BackupLeft == null && done.FileCount == 2, "move completed and old copy removed");
    Assert(Reparse.Read(src).Tag == Reparse.Mount && Reparse.Read(src).Target == dst, "original path is native /J junction");
    Assert(File.ReadAllText(Path.Combine(src, "a.txt")) == "目录连接点测试 🦆" &&
           File.Exists(Path.Combine(dst, "nested", "file.bin")) && Directory.Exists(Path.Combine(dst, "empty")),
           "source alias and destination data match");
    Reparse.Delete(Reparse.Read(src));

    string src2 = Path.Combine(scratch, "case2", "A");
    string dest2 = Path.Combine(scratch, "case2", "B", "A");
    Directory.CreateDirectory(src2);
    Directory.CreateDirectory(Path.GetDirectoryName(dest2)!);
    File.WriteAllText(Path.Combine(src2, "locked.txt"), "locked");
    using (var locked = new FileStream(Path.Combine(src2, "locked.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        await MustFail("locked file fails without moving source", src2, dest2);

    string src3 = Path.Combine(scratch, "case3", "A");
    string dest3 = Path.Combine(scratch, "case3", "B", "A");
    Directory.CreateDirectory(src3);
    Directory.CreateDirectory(dest3);
    await MustFail("target already exists fails without moving source", src3, dest3, targetPreexists: true);

    string src4 = Path.Combine(scratch, "case4", "A");
    string dest4 = Path.Combine(scratch, "case4", "B", "A");
    string nestedJunction = Path.Combine(src4, "inner-link");
    Directory.CreateDirectory(src4);
    Directory.CreateDirectory(Path.GetDirectoryName(dest4)!);
    Reparse.Create(nestedJunction, parent, Reparse.Mount);
    await MustFail("nested reparse point fails without touching source", src4, dest4);
    Reparse.Delete(Reparse.Read(nestedJunction));

    string src5 = Path.Combine(scratch, "case5", "A");
    string dest5 = Path.Combine(scratch, "case5", "B", "A");
    Directory.CreateDirectory(src5);
    Directory.CreateDirectory(Path.GetDirectoryName(dest5)!);
    File.WriteAllText(Path.Combine(src5, "read-only.txt"), "readonly");
    File.SetAttributes(Path.Combine(src5, "read-only.txt"), FileAttributes.ReadOnly);
    await MustFail("read-only file preflight rejects move", src5, dest5);
    File.SetAttributes(Path.Combine(src5, "read-only.txt"), FileAttributes.Normal);

    string src6 = Path.Combine(scratch, "case6", "A");
    string dest6 = Path.Combine(scratch, "case6", "B", "A");
    Directory.CreateDirectory(src6);
    Directory.CreateDirectory(Path.GetDirectoryName(dest6)!);
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel();
        bool wasCancelled = false;
        try { await MigrationEngine.MoveAsync(src6, dest6, new Progress<MoveProgress>(), cancelled.Token); }
        catch (OperationCanceledException) { wasCancelled = true; }
        Assert(wasCancelled && Directory.Exists(src6) && !Directory.Exists(dest6), "pre-cancel preserves source");
    }

    string src7 = Path.Combine(scratch, "case7", "A");
    string dest7 = Path.Combine(scratch, "case7", "B", "A");
    Directory.CreateDirectory(src7);
    Directory.CreateDirectory(Path.GetDirectoryName(dest7)!);
    File.WriteAllText(Path.Combine(src7, "symlink.txt"), "symbolic link test");
    try
    {
        await MigrationEngine.MoveAsync(src7, dest7, new Progress<MoveProgress>(),
            CancellationToken.None, Reparse.Symlink);
        Assert(Reparse.Read(src7).Tag == Reparse.Symlink &&
               File.ReadAllText(Path.Combine(src7, "symlink.txt")) == "symbolic link test",
               "directory symbolic link /D after verified move");
        Reparse.Delete(Reparse.Read(src7));
    }
    catch (Win32Exception ex) when (ex.NativeErrorCode is 5 or 1314)
    {
        Assert(Directory.Exists(src7) && !Directory.Exists(dest7),
            "symlink privilege denied; original folder safely restored");
    }

    Console.WriteLine($"ALL {passed} NATIVE TESTS PASSED");
}
finally
{
    // Explicit temp-only cleanup; never recurse through an unrecognized reparse point.
    foreach (string child in Directory.EnumerateDirectories(scratch, "*", SearchOption.AllDirectories)
             .OrderByDescending(s => s.Length))
    {
        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Refusing test cleanup: unexpected reparse point: " + child);
    }
    Directory.Delete(scratch, recursive: true);
}
