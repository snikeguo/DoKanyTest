using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ExplorerService
{
    public class WindowsExplorerService : IExplorerService
    {
        private DirectoryInfo currentDirectory = new DirectoryInfo() { FullName = "\\E\\", ItemType = ItemType.Directory };
        public Task<DirectoryInfo> GetCurrentDirectory()
        {
            currentDirectory.FullName = Directory.GetCurrentDirectory();
            return Task.FromResult(currentDirectory);
        }
        public Task SetCurrentDirectory(DirectoryInfo di)
        {
            currentDirectory = di;
            if(di.FullName!=rootDirectory.FullName) Directory.SetCurrentDirectory(di.FullName);
            return Task.CompletedTask;
        }
        
        public static string UnixPathStyleToWindowsPathStyle(string fn)
        {
            fn=fn.Substring(1);
            if(fn!=string.Empty)
            {
                fn=fn.Insert(1, ":");
            }
            return fn;
        }
        public static string WindowsPathStyleToUnixPathStyle(string fn)
        {
            fn = "\\" + fn;
            fn=fn.Replace(":","");
            return fn;
        }
        private DirectoryInfo rootDirectory = new DirectoryInfo() { FullName = "\\", ItemType = ItemType.Directory };
        public Task<DirectoryInfo> GetRootDirectory()
        {
            return Task.FromResult(rootDirectory);
        }


        public Task<string> GetSeparator()
        {
            return Task.FromResult("\\");
        }

        public Task CloseFile(object handle)
        {
            FileStream fs= handle as FileStream;
            fs.Close();
            return Task.CompletedTask;
        }

        public Task Dispose()
        {
            throw new NotImplementedException();
        }

        public Task FlushFile(object handle)
        {
            FileStream fs= handle as FileStream;
            return fs.FlushAsync();
        }


        public Task GetFreeSpace(ref UInt64 freeBytes, ref UInt64 AllBytes)
        {
            freeBytes = 0;
            AllBytes = 0;
            return Task.CompletedTask;
        }

        public Task<object> Init()
        {
            object n=null;
            return Task.FromResult(n);
        }

        public Task MkDir(string FullName)
        {
            FullName=UnixPathStyleToWindowsPathStyle(FullName);
            if (Directory.Exists(FullName) == true)
            {
                return Task.FromResult<bool>(false);
            }
            var di = Directory.CreateDirectory(FullName);
            return Task.CompletedTask;

        }

        public Task Mount(object arg)
        {
            return Task.CompletedTask;
        }

        public Task<Object> OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
        {
            path= UnixPathStyleToWindowsPathStyle(path);
            FileStream fileStream = new FileStream(path, mode, access, share);
            return Task.FromResult((object)fileStream);
        }

        public async Task<ulong> ReadFile(object handle, byte[] buf, long offset, long len)
        {
            FileStream fileStream=handle as FileStream;
            var re=await fileStream.ReadAsync(buf, (int)offset, (int)len);
            return (ulong)re;
        }

        public Task ReName(ItemInfo oldItemInfo, string ne)
        {
            var fn = UnixPathStyleToWindowsPathStyle(oldItemInfo.FullName);
            var win_new=UnixPathStyleToWindowsPathStyle(ne);
            if (oldItemInfo.ItemType==ItemType.Directory)
            {
                Directory.Move(fn, win_new);
            }
            else
            {
                File.Move(fn, win_new);
            }
            return Task.CompletedTask;
        }

        public Task Seek(object handle, uint offset)
        {
            throw new NotImplementedException();
        }

        public Task FlushFileSystem()
        {
            return Task.CompletedTask;
        }

        public Task UnlinkItem(ItemInfo itemInfo)
        {
            var fn = UnixPathStyleToWindowsPathStyle(itemInfo.FullName);
            if (itemInfo.ItemType== ItemType.Directory)
            {
                Directory.Delete(fn, true);
            }
            else
            {
                File.Delete(fn);
            }
            return Task.CompletedTask;
        }

        public Task<ulong> WriteFile(object handle, byte[] buf, long offset, long len)
        {
            FileStream fs = handle as FileStream;
            fs.Write(buf,(int) offset, (int)len);
            return Task.FromResult((ulong)len);
        }

        public Task Format(object s)
        {
            return Task.CompletedTask;
        }


        public Task<IEnumerable<ItemInfo>> GetSubItemInfos(string fullName)
        {
            string[] dirs = null;
            bool isRoot = false;
            if(fullName== rootDirectory.FullName)
            {
                var drv= DriveInfo.GetDrives();
                dirs=new string[drv.Length];
                for (int i = 0; i < dirs.Length; i++)
                {
                    dirs[i] = drv[i].Name;
                }
                isRoot = true;
            }
            else
            {
                fullName=UnixPathStyleToWindowsPathStyle(fullName);
                dirs = Directory.GetDirectories(fullName+"\\");
            }
            List<ItemInfo> infos = new List<ItemInfo>();
            foreach (var d in dirs)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo();
                directoryInfo.FullName = WindowsPathStyleToUnixPathStyle(d);
                directoryInfo.ItemType = ItemType.Directory;
                directoryInfo.TimeStamp = DateTime.MinValue;
                infos.Add(directoryInfo);
            }
            if(isRoot==false)
            {
                var files = Directory.GetFiles(fullName + "\\");
                foreach (var file in files)
                {
                    FileInfo fileInfo = new FileInfo();
                    fileInfo.FullName = WindowsPathStyleToUnixPathStyle(file);
                    fileInfo.ItemType = ItemType.File;
                    System.IO.FileInfo fileInfo1 = new System.IO.FileInfo(file);
                    fileInfo.Size = fileInfo1.Length;
                    fileInfo.TimeStamp= fileInfo1.LastWriteTime;
                    infos.Add(fileInfo);
                }
            }
            
            return Task.FromResult(infos as IEnumerable<ItemInfo>);
        }

        void IDisposable.Dispose()
        {
            //throw new NotImplementedException();
        }
                 
        public Task<ItemInfo> GetItemInfo(string fullName)
        {
            var wpath=UnixPathStyleToWindowsPathStyle(fullName);
            if (fullName =="\\")
            {
                DirectoryInfo dir = new DirectoryInfo();
                dir.FullName = fullName;
                dir.ItemType = ItemType.Directory;
                dir.CanRead = true;
                dir.CanWrite = true;
                dir.TimeStamp = DateTime.MinValue;
                return Task.FromResult(dir as ItemInfo);
            }
            if (Directory.Exists(wpath))
            {
                DirectoryInfo dir = new DirectoryInfo();
                dir.FullName = fullName;
                dir.ItemType = ItemType.Directory;
                dir.CanRead = true;
                dir.CanWrite = true;
                dir.TimeStamp = DateTime.MinValue;
                return Task.FromResult(dir as ItemInfo);
            }
            else if(File.Exists(wpath))
            {
                FileInfo file = new FileInfo();
                file.FullName = fullName;
                file.ItemType = ItemType.File;
                file.CanRead = true;
                file.CanWrite = true;
                file.TimeStamp = DateTime.MinValue;
                file.Size = new System.IO.FileInfo(wpath).Length;
                return Task.FromResult(file as ItemInfo);
            }
            ItemInfo itemInfo = null;
            return Task.FromResult(itemInfo);
        }
    }
}