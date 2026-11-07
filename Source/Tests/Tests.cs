using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using LinkShelf.Core;
static class Tests
{
    static int count;
    static void Check(bool ok,string name) { if(!ok)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);count++; }
    static void Throws(Action action,string name){try{action();}catch{Check(true,name);return;}throw new Exception("Expected failure: "+name);}
    static int Main()
    {
        try
        {
            var junction=Reparse.Parse(@"C:\缓存",Reparse.Build(Reparse.Mount,@"D:\数据\目录"));
            Check(junction.Target==@"D:\数据\目录" && junction.Tag==Reparse.Mount,"junction Unicode roundtrip");
            var symbolic=Reparse.Parse(@"C:\link",Reparse.Build(Reparse.Symlink,@"D:\target"));
            Check(symbolic.Target==@"D:\target" && !symbolic.Relative,"absolute symlink roundtrip");
            var network=Reparse.Parse(@"C:\link",Reparse.Build(Reparse.Symlink,@"\\nas\share\目录"));
            Check(network.Target==@"\\nas\share\目录","UNC symlink roundtrip");
            Throws(()=>Reparse.Parse("x",new byte[7]),"reject truncated header");
            var malformed=Reparse.Build(Reparse.Mount,@"D:\x");malformed[10]=255;
            Throws(()=>Reparse.Parse("x",malformed),"reject out-of-bounds target");
            Check(Paths.Contains(@"D:\data",@"D:\data\child"),"nested target detection");
            Check(!Paths.Contains(@"D:\data",@"D:\database"),"path-component boundary");
            var r1=new LinkRow{Path=@"C:\a",Target=@"D:\data"};
            var r2=new LinkRow{Path=@"C:\b",Target=@"d:\DATA"};
            var r3=new LinkRow{Path=@"C:\c",Target=@"D:\data\sub"};
            Check(Paths.UniqueRoots(new[]{r1,r2,r3}).Count==1,"same and nested target deduplication");
            Check(r1.Offloaded && !new LinkRow{Path=@"E:\a",Target=@"D:\data"}.Offloaded,"C-only transfer accounting");
            Check(!new LinkRow{Path=@"C:\a",Target=@"\\nas\x"}.Offloaded,"network excluded from transfer total");
            var alias=new LinkRow{Path=@"C:\alias",Target=@"E:\alias",Size=new SizeResult(1,1,0,0,true,"正常","",@"D:\data")};
            Check(Paths.UniqueRoots(new[]{r1,alias}).Count==1,"resolved alias deduplication");
            Check(!new LinkRow{Path=@"C:\a",Target=@"D:\alias",Size=new SizeResult(1,1,0,0,true,"正常","",@"C:\real")}.Offloaded,"alias back to C excluded");
            byte[] name=Encoding.Unicode.GetBytes("缓存\0"),parent=Encoding.Unicode.GetBytes("C:\\\0");
            byte[] packet=new byte[40+name.Length+parent.Length];
            void W(int off,uint v)=>BitConverter.GetBytes(v).CopyTo(packet,off);
            W(0,1);W(8,1);W(12,1);W(20,1);W(28,1);W(32,40);W(36,(uint)(40+name.Length));name.CopyTo(packet,40);parent.CopyTo(packet,40+name.Length);
            Check(EverythingIpc.Parse(packet).Single().EndsWith("缓存"),"IPC Unicode result decoding");
            W(32,uint.MaxValue);Throws(()=>EverythingIpc.Parse(packet),"IPC rejects invalid offsets");
            W(32,40);W(8,2);Throws(()=>EverythingIpc.Parse(packet),"IPC rejects truncated result set");
            Check(NaturalTextComparer.Instance.Compare("Folder2","Folder10")<0,"natural number ordering");
            Check(NaturalTextComparer.Instance.Compare("a999999999999999999999","a1000000000000000000000")<0,"large numeric names without overflow");
            Check(NaturalTextComparer.Instance.Compare("Cache2","cache2")==0,"case insensitive natural ordering");
            GroupingTest();
            PersistenceTest();
            if(OperatingSystem.IsWindows()) NativeTest();
            Console.WriteLine($"{count} checks passed; Windows native checks "+(OperatingSystem.IsWindows()?"executed":"pending on Windows"));
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void GroupingTest()
    {
        var old1=new LinkRow{Path=@"C:\old1"};
        var old2=new LinkRow{Path=@"C:\old2"};
        var fresh2=new LinkRow{Path=@"C:\new2"};
        var fresh1=new LinkRow{Path=@"C:\new1"};
        var existingOrder=new[]{old2,fresh2,old1,fresh1};
        var newSet=new HashSet<string>(StringComparer.OrdinalIgnoreCase){@"c:\NEW2",@"C:\new1"};
        Check(LinkOrdering.NewFirst(existingOrder,newSet).SequenceEqual(new[]{fresh2,fresh1,old2,old1}),
            "new links pinned first with both groups retaining column order");
        Check(LinkOrdering.NewFirst(existingOrder,new HashSet<string>()).SequenceEqual(existingOrder),
            "no new links preserves original sorting");
        Check(LinkOrdering.NewFirst(new[]{fresh2,fresh1},newSet).SequenceEqual(new[]{fresh2,fresh1}),
            "all-new list preserves sorting without a divider");
    }
    static void PersistenceTest()
    {
        string directory=Path.Combine(Path.GetTempPath(),"LinkShelf_PersistTest_"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store=new PersistentStore(directory);
            Check(store.CreatedUtc==null && store.PreviousLinks("instance A")==null,"first launch has no size cache or link baseline");
            var measured=new SizeResult(1048576,12,0,2,true,"正常 · 跳过内部链接","",@"D:\real");
            Check(store.SetSize(@"D:\cache",measured),"local size cache accepted");
            Check(!store.SetSize(@"\\nas\share",measured),"network size remains session-only");
            store.SaveSizes();
            var created=store.CreatedUtc;
            store.SaveLinks("instance A",new[]{@"C:\A",@"c:\a",@"C:\B"});
            store.SaveLinks("instance B",new[]{@"D:\other"});
            var reloaded=new PersistentStore(directory);
            Check(reloaded.Sizes.TryGetValue(@"d:\CACHE",out var saved) && saved==measured &&
                  !reloaded.Sizes.ContainsKey(@"\\nas\share") && reloaded.CreatedUtc==created,
                  "restart restores persistent size and creation timestamp");
            Check(reloaded.PreviousLinks("INSTANCE a") is { Count:2 } links && links.Contains(@"C:\a") &&
                  reloaded.PreviousLinks("instance B") is { Count:1 },"instance snapshots isolated and deduplicated");
            Check(PersistentStore.NewlyDiscovered(new[]{@"C:\A",@"C:\new",@"c:\NEW"},reloaded.PreviousLinks("instance A"))
                  .SetEquals(new[]{@"C:\new"}),"new links highlighted by path only");
            Check(PersistentStore.NewlyDiscovered(new[]{@"C:\existing"},null).Count==0,
                  "first launch never marks all links new");
            reloaded.SaveLinks("instance A",Array.Empty<string>());
            Check(new PersistentStore(directory).PreviousLinks("instance A") is { Count:2 },
                  "unexpected empty scan preserves nonempty baseline");
            var fresh=new PersistentStore(directory);
            fresh.SaveLinks("instance C",Array.Empty<string>());
            Check(new PersistentStore(directory).PreviousLinks("instance C") is { Count:0 },
                  "first empty scan creates real baseline");
            File.WriteAllText(Path.Combine(directory,"LinkShelf.links.json"),"{broken");
            var broken=new PersistentStore(directory);
            Check(broken.SnapshotLoadFailed && broken.PreviousLinks("instance A")==null &&
                  broken.Sizes.ContainsKey(@"D:\cache"),"bad snapshot cannot erase size cache");
            File.WriteAllText(Path.Combine(directory,"LinkShelf.sizes.json"),"{broken");
            var brokenSize=new PersistentStore(directory);
            Check(brokenSize.SizeLoadFailed && brokenSize.Sizes.Count==0,"bad size cache ignored safely");
        }
        finally { if(Directory.Exists(directory))Directory.Delete(directory,true); }
    }
    static void NativeTest()
    {
        string root=Path.Combine(Path.GetTempPath(),"LinkShelf_Test_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string a=Path.Combine(root,"targetA"),b=Path.Combine(root,"targetB"),link=Path.Combine(root,"link");
            Directory.CreateDirectory(a);Directory.CreateDirectory(b);File.WriteAllText(Path.Combine(a,"keep.txt"),"keep");
            Reparse.Create(link,a,Reparse.Mount);var row=Reparse.Read(link);
            Check(row.Target==a,"native create/read junction");
            Reparse.Retarget(row,b);Throws(()=>Reparse.Delete(row),"stale link snapshot rejected");
            row=Reparse.Read(link);Check(row.Target==b,"native retarget junction");
            Reparse.Delete(row);Check(!Directory.Exists(link)&&Directory.Exists(b)&&File.ReadAllText(Path.Combine(a,"keep.txt"))=="keep","native delete preserves targets");
            Throws(()=>Reparse.Create(a,b,Reparse.Mount),"existing ordinary directory never reused");
            var size=SizeScanner.Scan(a,default);Check(size.Complete&&size.Bytes==4&&size.Files==1,"native target size");
            Reparse.Create(link,a,Reparse.Mount);Directory.Delete(a,true);
            Check(Reparse.Read(link).Target==a,"broken junction target remains readable");
            Reparse.Delete(Reparse.Read(link));Check(!Directory.Exists(link),"broken junction deletion");
        }
        finally { if(Directory.Exists(root))Directory.Delete(root,true); }
    }
}
