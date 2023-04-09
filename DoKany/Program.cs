using DokanNet;
using DokanNet.Logging;
using ExplorerService;
using System;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Text;

namespace DoKanyForFatFs
{
    public class Program
    {
        public static void Main()
        {
            try
            {
                var dokanLogger = new ConsoleLogger("[Dokan] ");
                var fullLogger = new Logger(dokanLogger.Debug, dokanLogger.Info, dokanLogger.Warn, dokanLogger.Error, dokanLogger.Fatal);
                using (var mre = new System.Threading.ManualResetEvent(false))
                using (var dokan = new Dokan(fullLogger))
                {
                    Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) =>
                    {
                        e.Cancel = true;
                        mre.Set();
                    };
                    var es = new WindowsExplorerService();
                    var pm = new PathManager(es);
                    var rfs = new DoKanyForFatFs(pm,fullLogger);
                    var dokanBuilder = new DokanInstanceBuilder(dokan)
                        .ConfigureOptions(options =>
                        {
                            options.Options = DokanOptions.DebugMode | DokanOptions.StderrOutput;
                            options.MountPoint = "X:\\";
                        });
                    using (var dokanInstance = dokanBuilder.Build(rfs))
                    {
                        mre.WaitOne();
                    }
                    Console.WriteLine(@"Success");
                }
            }
            catch (DokanException ex)
            {
                Console.WriteLine(@"Error: " + ex.Message);
            }
        }
    }
    public class DoKanyForFatFs : IDokanOperations
    {
        public PathManager PathManager { get;set; }
        public IExplorerService ExplorerService { get;set; }
        private bool IsMounted = false;
        public ILogger Logger { get; set; } 

        public DoKanyForFatFs(PathManager pm,ILogger logger)
        {
            Logger = logger;
            PathManager = pm;
            ///todo:这里挂载设备
            PathManager.Init();
            ExplorerService = pm.FsExplorer;
            IsMounted = true;
        }
        private void MyDebug(string s)
        {
            Logger.Warn(s);

        }
        public void Cleanup(string fileName, IDokanFileInfo info)
        {
            MyDebug($"[David]Cleanup:{fileName},DeleteOnClose:{info.DeleteOnClose}");
        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {
            MyDebug($"[David]CloseFile:{fileName},DeleteOnClose:{info.DeleteOnClose}");
            if (info.Context != null)
            {
                ExplorerService.CloseFile(info.Context);
            }
            if (info.DeleteOnClose==true)
            {
                var itemInfo = ExplorerService.GetItemInfo(fileName).Result;
                if(itemInfo != null)//有可能调用了DeleteDirectory
                {
                    ExplorerService.UnlinkItem(itemInfo).Wait();
                }
            }
        }

        public NtStatus CreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, IDokanFileInfo info)
        {
            MyDebug($"[David]CreateFile:{fileName},mode:{mode}");


            if (mode == FileMode.CreateNew || mode == FileMode.Create || mode == FileMode.OpenOrCreate)
            {
                if (info.IsDirectory == true)
                {
                    ExplorerService.MkDir(fileName).Wait();
                }
                else
                {
                    MyDebug("[David]CreateFile:CreateNewFile");
                    var handle = ExplorerService.OpenFile(fileName, mode, System.IO.FileAccess.ReadWrite, share).Result;
                    info.Context = handle;
                }
            }
            else if(mode== FileMode.Open)
            {
                var itemInfo = ExplorerService.GetItemInfo(fileName).Result;
                if (itemInfo == null && mode== FileMode.Open)
                {
                    return NtStatus.NoSuchFile;
                }
                else if(itemInfo.ItemType== ItemType.File)
                {
                    MyDebug("[David]CreateFile:OpenFile");
                    var handle = ExplorerService.OpenFile(fileName, mode, System.IO.FileAccess.Read,share).Result;
                    info.Context = handle;
                    info.IsDirectory = false;
                }
                
            }

            return DokanResult.Success;

        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            //这里删除子文件和文件夹
            var itemInfo=ExplorerService.GetItemInfo(fileName).Result;
            ExplorerService.UnlinkItem(itemInfo);
            return DokanResult.Success;
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            //var itemInfo = ExplorerService.GetItemInfo(fileName).Result;
            //ExplorerService.UnlinkItem(itemInfo).Wait();
            //这里判断file name能不能被删除，如果返回成功，则可以删除，会调用close函数删除。
            return DokanResult.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            MyDebug($"[David]FindFiles:{fileName}");
            files = new List<FileInformation>();
            try
            {
                var dirInfo = ExplorerService.GetItemInfo(fileName).Result;
                List<ItemInfo> items = null;
                if (dirInfo.ItemType == ItemType.File)
                {
                    info.IsDirectory = false;
                    items=new List<ItemInfo>();
                    items.Add(dirInfo);
                    MyDebug($"[David]FindFiles:this is a file:{dirInfo.FullName},size:{dirInfo.Size}");
                }
                else
                {
                    info.IsDirectory = true;
                    items = ExplorerService.GetSubItemInfos(fileName).Result as List<ItemInfo>;
                }
               

                foreach (var item in items)
                {
                    var dfi = new FileInformation();
                    dfi.FileName = PathManager.GetItemName(item.FullName);
                    dfi.LastAccessTime = DateTime.Now;
                    dfi.CreationTime = null;
                    dfi.LastWriteTime = null;
                    MyDebug($"[David]FindFiles:add item:{item.FullName},size:{item.Size}");
                    if (item.ItemType == ItemType.Directory)
                    {
                        dfi.Attributes = FileAttributes.Directory;
                        dfi.Length = 0;
                    }
                    else
                    {
                        dfi.Attributes = FileAttributes.Normal;
                        dfi.Length = item.Size;
                    }
                    files.Add(dfi);
                }
            }
            catch (Exception)
            {

                return NtStatus.Error;
            }
            
            return NtStatus.Success;
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = new FileInformation[0];
            return DokanResult.NotImplemented;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            streams = new FileInformation[0];
            return DokanResult.NotImplemented;
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
        {
            freeBytesAvailable = 512 * 1024 * 1024;
            totalNumberOfBytes = 1024 * 1024 * 1024;
            totalNumberOfFreeBytes = 512 * 1024 * 1024;
            return DokanResult.Success;
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            fileInfo = new FileInformation { FileName = fileName };
            MyDebug($"[David]GetFileInformation:{fileName}");
            try
            {
                var itemInfo = ExplorerService.GetItemInfo(fileName).Result;
                if(itemInfo != null)
                {
                    fileInfo.LastAccessTime = DateTime.Now;
                    fileInfo.LastWriteTime = null;
                    fileInfo.CreationTime = null;
                    if (itemInfo.ItemType == ItemType.Directory)
                    {
                        fileInfo.Attributes = FileAttributes.Directory;
                        //info.IsDirectory = true;
                    }
                    else
                    {
                        fileInfo.Attributes = FileAttributes.Normal;
                        info.IsDirectory = false;
                        //fileInfo.Length = itemInfo.Size;
                        MyDebug($"[David]GetFileInformation:this is a file:{itemInfo.FullName},size:{itemInfo.Size}");
                    }
                    return DokanResult.Success;
                }
                else
                {
                    return NtStatus.Error;
                }
            }
            catch (Exception)
            {

                return NtStatus.Error;
            }
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
            security = null;
            return DokanResult.Error;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
        {
            volumeLabel = "RFS";
            features = FileSystemFeatures.None;
            fileSystemName = string.Empty;
            maximumComponentLength = 256;
            return DokanResult.Error;
        }

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
        {
            return NtStatus.Success;
        }
        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            MyDebug($"[David]MoveFile:oldName:{oldName},newName:{newName}");
            var itemInfo = ExplorerService.GetItemInfo(oldName).Result;
            ExplorerService.ReName(itemInfo, newName);
            return NtStatus.Success;
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            
            bytesRead =(int)ExplorerService.ReadFile(info.Context, buffer, 0, buffer.LongLength).Result;
            MyDebug($"[David]ReadFile:{fileName},buffer length:{buffer.LongLength},offset:{offset},bytesRead:{bytesRead}");
            return DokanResult.Success;
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }


        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            //设置文件大小
            MyDebug($"[David]SetEndOfFile:{fileName},length:{length}");
            return NtStatus.Success;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
        {
            return NtStatus.Error;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, IDokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus Unmounted(IDokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)//这个的offset是文件的offset不是buffer的offset
        {
            MyDebug($"[David]WriteFile:{fileName}");
            bytesWritten= (int)ExplorerService.WriteFile(info.Context, buffer, 0, buffer.LongLength).Result;
            return NtStatus.Success;
        }
    }
}

