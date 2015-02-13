using StorjClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly string lockTask = Guid.NewGuid().ToString();
        private Task<Stream> downloadTask = null;

        public string Id { get { return lockTask; } }

        public FileDownloader(string hash, string key, string apiUrl)
        {
            this.hash = hash;
            this.key = key;
            this.apiUrl = apiUrl;
        }

        public async Task<int> Read(byte[] buffer, long offset)
        {
            EnsureDownloadStarted();

            Trace.WriteLine("Type: " + downloadTask.Result.GetType());

            if (downloadTask.Result.CanSeek)
            {
                downloadTask.Result.Seek(offset, SeekOrigin.Begin);

                return await downloadTask.Result.ReadAsync(buffer, 0, buffer.Length);
            }

            int read = await downloadTask.Result.ReadAsync(buffer, 0, buffer.Length);
            int additionalRead = read;

            // Read until buffer is full or nothing more to read
            while (read < buffer.Length && additionalRead > 0 && downloadTask.Result.CanRead)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(3000));
             
                byte[] additionalBuffer = new byte[buffer.Length - read];

                additionalRead = await downloadTask.Result.ReadAsync(additionalBuffer, 0, additionalBuffer.Length);

                Array.Copy(additionalBuffer, 0, buffer, read, additionalRead);

                read += additionalRead;
            }

            if (additionalRead == 0)
            {
                downloadTask.Result.Close();
                downloadTask = null;
            }

            return read;
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
