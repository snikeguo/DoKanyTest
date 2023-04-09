using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExplorerService
{
    public enum ItemType
    {
        Directory,
        File,
    }
    public class ItemInfo
    {
        public string FullName { get; set; }
        public ItemType ItemType { get; set; }
        public bool CanRead { get; set; }
        public bool CanWrite { get; set; }
        public DateTime TimeStamp { get; set; }

        public long Size { get; set; }

    }
    public class DirectoryInfo :ItemInfo
    {

    }
    public class FileInfo : ItemInfo
    {   
        
    }
    //like:windows explorer、littlefs、fatfs、
    public interface IExplorerService:IDisposable
    {
        Task<DirectoryInfo> GetCurrentDirectory();
        Task SetCurrentDirectory(DirectoryInfo di);


        Task<DirectoryInfo> GetRootDirectory();
        Task<string> GetSeparator();//window :\\ unix:/

        Task<IEnumerable<ItemInfo>> GetSubItemInfos(string fullName);

        Task<ItemInfo>        GetItemInfo(string fullName);


        Task<object> Init();
        Task MkDir(string FullName);
        Task Format(object arg);//format fs
        Task Mount(object arg);
        Task GetFreeSpace(ref UInt64 freeBytes, ref UInt64 AllBytes);

        Task UnlinkItem(ItemInfo itemInfo);//delete fs/dir

        Task<object> OpenFile(string path, FileMode mode, FileAccess access, FileShare share);
        Task<ulong> WriteFile(object handle,byte[] buf, long offset, long len);
        Task<ulong> ReadFile(object handle, byte[] buf, long offset, long len);

        Task CloseFile(object handle);

        Task Seek(object handle, uint offset);

        Task FlushFile(object handle);

        Task FlushFileSystem();

        Task ReName(ItemInfo oldItemInfo, string _new);

    }
    public interface IRamFileSystemExplorerService
    {
        Task SetRamDiskContent(string fn);
        Task<UInt64> GetRamDiskSize();
        Task<double> GetCacheHidRate();

        Task WriteToFile(string fn);
    }

    public class PathManager
    {
        public IExplorerService FsExplorer { get; set; }

        public DirectoryInfo RootDirectory { get; private set; }
        public string Separator { get; private set; }

        public  async void Init()
        {
            RootDirectory = await FsExplorer.GetRootDirectory();
            Separator=await FsExplorer.GetSeparator();
        }
        public PathManager(IExplorerService fsExplorer)
        {
            FsExplorer = fsExplorer;
        }

        public string GetItemName(string fn)
        {
            if(CheckPath(fn)== false)
            {
                throw new Exception("路径开头必须是root dir!");
            }
            if (IsRootPath(fn))
                return String.Empty;
            if (fn[fn.Length - 1].ToString() == Separator)
                fn=fn.Remove(fn.Length - 1);
            string name = fn;
            if(fn.Contains(Separator))
            {
                name = fn.Split(Separator).Last();
            }
            else if(fn==string.Empty ||fn ==RootDirectory.FullName)
            {
                name = RootDirectory.FullName;
            }
            if(name==string.Empty)
            {
                throw new Exception(); 
            }
            return name;
        }
        public string GetParent(string fn)
        {
            if (CheckPath(fn) == false)
            {
                throw new Exception("路径开头必须是root dir!");
            }
            if(IsRootPath(fn))
                return RootDirectory.FullName;
            var index = fn.LastIndexOf(GetItemName(fn));
            index -= 1;
            if(index<=0)
            {
                return RootDirectory.FullName;
            }
            return fn.Substring(0, index);
        }
        private bool CheckPath(string fn)
        {
            if (fn.StartsWith(RootDirectory.FullName) == false)
            {
                return false;
            }
            return true;
        }
        public bool IsRootPath(string fn)
        {
            if (CheckPath(fn) == false)
                throw new Exception();
            if (fn == RootDirectory.FullName)
                return true;
            return false;
        }
        public async Task<IEnumerable<DirectoryInfo>> GetDirectoryInfoRelativeToRoot(string fullName)
        {
            List<DirectoryInfo> directoryInfos = new List<DirectoryInfo>();
            if (CheckPath(fullName) == false)
                throw new Exception();
            do
            {
                if (IsRootPath(fullName))
                {
                    var root = await FsExplorer.GetItemInfo(fullName);
                    directoryInfos.Add(root as DirectoryInfo);
                    break;
                }
                else
                {
                    var dir = await FsExplorer.GetItemInfo(fullName);
                    directoryInfos.Add(dir as DirectoryInfo);
                }
                fullName = GetParent(fullName);
            } while (true);
            directoryInfos.Reverse();
            return directoryInfos; 
        }

    }
#if false
    class Program
    {
        static void Main(string[] args)
        {
            Task.Run(async () =>
            {
                FatFsExplorerService fatFsExplorerService = new FatFsExplorerService();
                IExplorerService explorerService = fatFsExplorerService as IExplorerService;
                PathManager pathManager = new PathManager(explorerService);
                pathManager.Init();
                var cwd = await explorerService.GetCurrentDirectory();
    

                var name5 = pathManager.GetItemName("/1.txt");
                var name6 = pathManager.GetItemName("/1.txt/");
                var name7 = pathManager.GetItemName("/mydir");

                var name8 = pathManager.GetItemName("/test/我的电脑");
            });

            Thread.Sleep(-1);
        }
    }
#endif
}
