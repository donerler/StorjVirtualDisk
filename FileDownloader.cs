using StorjClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StorjVirtualDisk
{
    public class FileDownloader
    {
        private readonly string hash;
        private readonly string key;
        private readonly string apiUrl;
        private readonly object lockTask = new object();
        private Task<Stream> downloadTask = null;

        public FileDownloader(string hash, string key, string apiUrl)
        {
            this.hash = hash;
            this.key = key;
            this.apiUrl = apiUrl;
        }

        public int Read(byte[] buffer, long offset)
        {
            EnsureDownloadStarted();

            //downloadTask.Result.Seek(offset, SeekOrigin.Begin);

            return downloadTask.Result.Read(buffer, 0, buffer.Length);
        }

        private void EnsureDownloadStarted()
        {
            lock (lockTask)
            {
                if (downloadTask == null)
                {
                    StorjApiClient client = new StorjApiClient(apiUrl);

                    downloadTask = client.DownloadStreamedAsync(hash, key);
                }
            }
        }
    }
}
