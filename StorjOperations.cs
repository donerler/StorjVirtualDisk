using Dokan;
using Newtonsoft.Json;
using StorjClient;
using StorjClient.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StorjVirtualDisk
{
    public class StorjOperations : DokanOperations
    {
        private const string apiUrl = "http://node1.metadisk.org/";
        private readonly Action<bool> onCummunicate;
        private readonly Lazy<string> dataFile;
        private readonly Lazy<FileReferences> files;

        public StorjOperations(Action<bool> onCummunicate = null)
        {
            this.onCummunicate = onCummunicate;

            dataFile = new Lazy<string>(() => GetUserDataFile());
            files = new Lazy<FileReferences>(() => LoadFileReferences(dataFile.Value));
        }

        private static string GetUserDataFile()
        {
            string settingsFile =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StorjVirtualDisk", "data.dat");

            FileInfo file = new FileInfo(settingsFile);

            file.Directory.Create();

            return settingsFile;
        }

        private static FileReferences LoadFileReferences(string dataFile)
        {
            if (!File.Exists(dataFile))
            {
                return new FileReferences { Name = string.Empty };
            }

            try
            {
                IList<string> data = File.ReadAllText(dataFile).Split('|');

                StorjApiClient client = new StorjApiClient(apiUrl);

                byte[] fileListBytes = client.DownloadAsync(data.First(), data.Last()).Result;

                return JsonConvert.DeserializeObject<FileReferences>(Encoding.ASCII.GetString(fileListBytes));
            }
            catch 
            {
                return new FileReferences { Name = string.Empty };
            }
        }

        private void PersistFileReferences()
        {
            StartCommunication();

            string data = JsonConvert.SerializeObject(files.Value);

            StorjApiClient client = new StorjApiClient(apiUrl);

            try
            {
                UploadedFile cloudFile = client.UploadAsync(new MemoryStream(Encoding.ASCII.GetBytes(data)), "somefile.txt").Result;

                File.WriteAllText(dataFile.Value, string.Format("{0}|{1}", cloudFile.FileHash, cloudFile.Key));
            }
            catch (Exception e) 
            {
            }
            finally
            {
                EndCommunication();
            }
        }

        private void StartCommunication()
        {
            if (onCummunicate != null) onCummunicate(true);
        }

        private void EndCommunication()
        {
            if (onCummunicate != null) onCummunicate(false);
        }

        public int Cleanup(string filename, DokanFileInfo info)
        {
            WriteTrace("cleanup:", filename);

            return DokanNet.DOKAN_SUCCESS;
        }

        public int CloseFile(string filename, DokanFileInfo info)
        {
            WriteTrace("closefile", filename);

            return DokanNet.DOKAN_SUCCESS;
        }

        public int CreateDirectory(string filename, DokanFileInfo info)
        {
            WriteTrace("createdirectory", filename);

            FileReferences folderReference = files.Value.GetFolderReference(filename);

            string name = filename.Split(new [] {'\\'}, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

            if (folderReference == null || string.IsNullOrEmpty(name) || !IsContainerNameValid(name))
            {
                return -DokanNet.ERROR_INVALID_NAME;
            }

            if (folderReference.Name == name)
            {
                return -DokanNet.ERROR_ALREADY_EXISTS;
            }

            folderReference.Children.Add(new FileReferences { Name = name });

            PersistFileReferences();

            return DokanNet.DOKAN_SUCCESS;
        }

        public int CreateFile(string filename, FileAccess access, FileShare share, FileMode mode, FileOptions options, DokanFileInfo info)
        {
            WriteTrace("createfile", filename, access, share, mode, options);

            if (share == FileShare.Delete)
            {
                return DeleteFile(filename, info);
            }

            if (mode == FileMode.CreateNew)
            {
                return DokanNet.DOKAN_SUCCESS;
            }

            FileReferences fileReference = files.Value.GetFolderReference(filename);

            if (fileReference != null)
            {
                info.IsDirectory = fileReference.IsFolder();

                return DokanNet.DOKAN_SUCCESS;
            }
            else
            {
                return -DokanNet.ERROR_FILE_NOT_FOUND;
            }
        }

        public int DeleteDirectory(string filename, DokanFileInfo info)
        {
            WriteTrace("deletedirectory", filename);

            return DeleteFile(filename, info);
        }

        public int DeleteFile(string filename, DokanFileInfo info)
        {
            WriteTrace("deletefile", filename);

            FileReferences oldParentReference = files.Value.GetFolderReference(filename.Substring(0, filename.LastIndexOf('\\') + 1));

            if (oldParentReference == null)
            {
                return -DokanNet.ERROR_PATH_NOT_FOUND;
            }

            string name = filename.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            FileReferences oldReference = oldParentReference.Children.FirstOrDefault(child => child.Name == name);

            if (oldReference == null)
            {
                return -DokanNet.ERROR_PATH_NOT_FOUND;
            }

            oldParentReference.Children.Remove(oldReference);

            PersistFileReferences();

            return DokanNet.DOKAN_SUCCESS;
        }

        public int FindFiles(string filename, ArrayList fileList, DokanFileInfo info)
        {
            WriteTrace("findfiles", filename);

            FileReferences folderReference = files.Value.GetFolderReference(filename);

            if (folderReference == null || !folderReference.IsFolder())
            {
                return -DokanNet.ERROR_PATH_NOT_FOUND;
            }

            fileList.AddRange(folderReference.Children.Select(child => new Dokan.FileInformation
            {
                Attributes = child.IsFolder() ? FileAttributes.Directory : FileAttributes.Normal,
                FileName = child.Name,
                LastAccessTime = child.Date ?? DateTime.Now,
                LastWriteTime = child.Date ?? DateTime.Now,
                CreationTime = child.Date ?? DateTime.Now,
                Length = child.Size
            }).ToArray());

            return DokanNet.DOKAN_SUCCESS;
        }

        public int FlushFileBuffers(string filename, DokanFileInfo info)
        {
            WriteTrace("flushfilebuffers", filename);

            return DokanNet.DOKAN_SUCCESS;
        }

        public int GetDiskFreeSpace(ref ulong freeBytesAvailable, ref ulong totalBytes, ref ulong totalFreeBytes, DokanFileInfo info)
        {
            WriteTrace("getfreediskspace");

            freeBytesAvailable = 1024 * 1024 * 1024;
            totalBytes = 1024 * 1024 * 1024;
            totalFreeBytes = 1024 * 1024 * 1024;

            return DokanNet.DOKAN_SUCCESS;
        }

        public int GetFileInformation(string filename, Dokan.FileInformation fileinfo, DokanFileInfo info)
        {
            WriteTrace("getfileinformation", filename);

            FileReferences fileReference = files.Value.GetFolderReference(filename);

            string name = filename.Split(new [] {'\\'}, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

            if (fileReference != null && (fileReference.Name ?? string.Empty) != (name ?? string.Empty))
            {
                return DokanNet.DOKAN_SUCCESS;
            }

            if (fileReference != null)
            {
                fileinfo.Attributes = fileReference.IsFolder() ? FileAttributes.Directory : FileAttributes.Normal;
                fileinfo.LastAccessTime = fileReference.Date ?? DateTime.Now;
                fileinfo.LastWriteTime = fileReference.Date ?? DateTime.Now;
                fileinfo.CreationTime = fileReference.Date ?? DateTime.Now;
                fileinfo.FileName = fileReference.Name;
                fileinfo.Length = fileReference.Size;

                return DokanNet.DOKAN_SUCCESS;
            }

            return -DokanNet.ERROR_FILE_NOT_FOUND;
        }

        public int LockFile(string filename, long offset, long length, DokanFileInfo info)
        {
            WriteTrace("lockfile", filename, offset, length);

            return DokanNet.DOKAN_SUCCESS;
        }

        public int MoveFile(string filename, string newname, bool replace, DokanFileInfo info)
        {
            WriteTrace("movefile", filename, newname, replace);

            string name = newname.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

            if (!IsContainerNameValid(name))
            {
                return -DokanNet.ERROR_INVALID_NAME;
            }

            FileReferences oldReference = files.Value.GetFolderReference(filename);

            if (oldReference == null)
            {
                return -DokanNet.ERROR_PATH_NOT_FOUND;
            }

            FileReferences newReference = files.Value.GetFolderReference(newname);

            if (newReference == null || newReference.Name == name)
            {
                return -DokanNet.ERROR_ALREADY_EXISTS;
            }

            FileReferences oldParentReference = files.Value.GetFolderReference(filename.Substring(0, filename.LastIndexOf('\\') + 1));

            newReference.Children.Add(oldReference);
            oldParentReference.Children.Remove(oldReference);
            oldReference.Name = name;

            PersistFileReferences();

            return DokanNet.DOKAN_SUCCESS;
        }

        public int OpenDirectory(string filename, DokanFileInfo info)
        {
            WriteTrace("opendirectory", filename);

            return DokanNet.DOKAN_SUCCESS;
        }

        public int ReadFile(string filename, byte[] buffer, ref uint readBytes, long offset, DokanFileInfo info)
        {
            WriteTrace("readfile", filename);

            string name = filename.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

            FileReferences fileReference = files.Value.GetFolderReference(filename);

            if (fileReference == null || fileReference.Name != name)
            {
                return -DokanNet.ERROR_FILE_NOT_FOUND;
            }

            try
            {
                StartCommunication();

                StorjApiClient client = new StorjApiClient(apiUrl);

                using (MemoryStream stream = new MemoryStream(client.DownloadAsync(fileReference.Hash, fileReference.Key).Result))
                {
                    stream.Seek(offset, SeekOrigin.Begin);
                    readBytes = (uint)stream.Read(buffer, 0, buffer.Length);
                }
            }
            catch
            {
                return -DokanNet.ERROR_FILE_NOT_FOUND;
            }
            finally
            {
                EndCommunication();
            }

            return DokanNet.DOKAN_SUCCESS;
        }

        public int SetAllocationSize(string filename, long length, DokanFileInfo info)
        {
            WriteTrace("setallocationsize", filename);

            return DokanNet.DOKAN_SUCCESS;
        }

        public int SetEndOfFile(string filename, long length, DokanFileInfo info)
        {
            WriteTrace("setendoffile", filename, length);

            return DokanNet.DOKAN_SUCCESS;
        }

        public int SetFileAttributes(string filename, FileAttributes attr, DokanFileInfo info)
        {
            WriteTrace("setfileattributes", filename, attr);

            return DokanNet.DOKAN_SUCCESS;
        }

        public int SetFileTime(string filename, DateTime ctime, DateTime atime, DateTime mtime, DokanFileInfo info)
        {
            WriteTrace("setfiletime", filename, ctime, atime, mtime);

            return DokanNet.DOKAN_SUCCESS;
        }

        public int UnlockFile(string filename, long offset, long length, DokanFileInfo info)
        {
            WriteTrace("unlockfile", filename, offset, length);

            return DokanNet.DOKAN_SUCCESS;
        }

        public int Unmount(DokanFileInfo info)
        {
            WriteTrace("unmount");

            return DokanNet.DOKAN_SUCCESS;
        }

        public int WriteFile(string filename, byte[] buffer, ref uint writtenBytes, long offset, DokanFileInfo info)
        {
            WriteTrace("writefile", filename);

            try
            {
                StartCommunication();

                string name = filename.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

                FileReferences fileReference = files.Value.GetFolderReference(filename);

                if (fileReference == null)
                {
                    return -DokanNet.ERROR_PATH_NOT_FOUND;
                }

                StorjApiClient client = new StorjApiClient(apiUrl);
                UploadedFile cloudFile = client.UploadAsync(new MemoryStream(buffer), name).Result;

                if (fileReference.Name != name && fileReference.IsFolder())
                {
                    FileReferences newFileReference = new FileReferences { Name = name };
                    fileReference.Children.Add(newFileReference);

                    fileReference = newFileReference;
                }

                fileReference.Hash = cloudFile.FileHash;
                fileReference.Key = cloudFile.Key;
                fileReference.Date = DateTime.Now;
                fileReference.Size = buffer.Length;

                PersistFileReferences();
            }
            catch (Exception e)
            {
                return -DokanNet.ERROR_ACCESS_DENIED;
            }
            finally
            {
                EndCommunication();
            }

            return DokanNet.DOKAN_SUCCESS;
        }
        
        private static bool IsContainerNameValid(string containerName)
        {
            return (!containerName.Any(c => Path.GetInvalidPathChars().Contains(c)) && (3 <= containerName.Length) && (containerName.Length <= 63));
        }

        private static void WriteTrace(params object[] arguments)
        {
            Trace.WriteLine(string.Join("|", arguments));
        }
    }
}
