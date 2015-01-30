using Dokan;
using Newtonsoft.Json;
using StorjClient;
using StorjClient.Data;
using System;
using System.Collections;
using System.Collections.Generic;
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
            return DokanNet.DOKAN_SUCCESS;
        }

        public int CloseFile(string filename, DokanFileInfo info)
        {
            return DokanNet.DOKAN_SUCCESS;
        }

        public int CreateDirectory(string filename, DokanFileInfo info)
        {
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
            return DeleteFile(filename, info);
        }

        public int DeleteFile(string filename, DokanFileInfo info)
        {
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
            FileReferences folderReference = files.Value.GetFolderReference(filename);

            if (folderReference == null || !folderReference.IsFolder())
            {
                return -DokanNet.ERROR_PATH_NOT_FOUND;
            }

            fileList.AddRange(folderReference.Children.Select(child => new Dokan.FileInformation
            {
                Attributes = child.IsFolder() ? FileAttributes.Directory : FileAttributes.Normal,
                FileName = child.Name,
                LastAccessTime = DateTime.Now,//child.Date ?? DateTime.Now,
                LastWriteTime = DateTime.Now,//child.Date ?? DateTime.Now,
                CreationTime = DateTime.Now,//child.Date ?? DateTime.Now,
                Length = child.Size
            }).ToArray());

            return DokanNet.DOKAN_SUCCESS;
        }

        public int FlushFileBuffers(string filename, DokanFileInfo info)
        {
            return DokanNet.DOKAN_SUCCESS;
        }

        public int GetDiskFreeSpace(ref ulong freeBytesAvailable, ref ulong totalBytes, ref ulong totalFreeBytes, DokanFileInfo info)
        {
            freeBytesAvailable = 1024 * 1024 * 1024;
            totalBytes = 1024 * 1024 * 1024;
            totalFreeBytes = 1024 * 1024 * 1024;

            return DokanNet.DOKAN_SUCCESS;
        }

        public int GetFileInformation(string filename, Dokan.FileInformation fileinfo, DokanFileInfo info)
        {
            FileReferences fileReference = files.Value.GetFolderReference(filename);

            string name = filename.Split(new [] {'\\'}, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

            if (fileReference != null && fileReference.Name != name)
            {
                return DokanNet.DOKAN_SUCCESS;
            }

            if (fileReference != null)
            {
                fileinfo.Attributes = fileReference.IsFolder() ? FileAttributes.Directory : FileAttributes.Normal;
                fileinfo.LastAccessTime = DateTime.Now;// fileReference.Date ?? DateTime.Now;
                fileinfo.LastWriteTime = DateTime.Now;//fileReference.Date ?? DateTime.Now;
                fileinfo.CreationTime = DateTime.Now;//fileReference.Date ?? DateTime.Now;
                fileinfo.FileName = fileReference.Name;
                fileinfo.Length = fileReference.Size;

                return DokanNet.DOKAN_SUCCESS;
            }

            return -DokanNet.ERROR_FILE_NOT_FOUND;
        }

        public int LockFile(string filename, long offset, long length, DokanFileInfo info)
        {
            return DokanNet.DOKAN_SUCCESS;
        }

        public int MoveFile(string filename, string newname, bool replace, DokanFileInfo info)
        {
            string name = newname.Split(new [] {'\\'}, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

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
            return DokanNet.DOKAN_SUCCESS;
        }

        public int ReadFile(string filename, byte[] buffer, ref uint readBytes, long offset, DokanFileInfo info)
        {
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
            return DokanNet.DOKAN_SUCCESS;
        }

        public int SetEndOfFile(string filename, long length, DokanFileInfo info)
        {
            return DokanNet.DOKAN_SUCCESS;
        }

        public int SetFileAttributes(string filename, FileAttributes attr, DokanFileInfo info)
        {
            return DokanNet.DOKAN_SUCCESS;
        }

        public int SetFileTime(string filename, DateTime ctime, DateTime atime, DateTime mtime, DokanFileInfo info)
        {
            return DokanNet.DOKAN_SUCCESS;
        }

        public int UnlockFile(string filename, long offset, long length, DokanFileInfo info)
        {
            return DokanNet.DOKAN_SUCCESS;
        }

        public int Unmount(DokanFileInfo info)
        {
            return DokanNet.DOKAN_SUCCESS;
        }

        public int WriteFile(string filename, byte[] buffer, ref uint writtenBytes, long offset, DokanFileInfo info)
        {
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
                //fileReference.Date = DateTime.Now;
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
    }
}
