using StorjClient;
using StorjFileEncryptor;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace StorjVirtualDisk
{
    public class DycryptedFileDownloader : FileDownloaderBase
    {
        private Lazy<Aes128CtrCryptoStream> cryptoStream = null;

        public DycryptedFileDownloader(string hash, string key, string apiUrl) 
            : base(hash, key, apiUrl)
        {
        }

        protected override async Task<int> ReadStreamAsync(Stream stream, byte[] buffer, string key)
        {
            if (cryptoStream == null)
            {
                cryptoStream = new Lazy<Aes128CtrCryptoStream>(() => new Aes128CtrCryptoStream(stream, key));
            }

            int read = await cryptoStream.Value.ReadAsync(buffer, 0, buffer.Length);

            if (read == 0)
            {
                cryptoStream.Value.Dispose();
                cryptoStream = null;
            }

            return read;
        }

        protected override async Task<Stream> DownloadStreamedAsync(StorjApiClient client, string hash, string key)
        {
            // Do not provide the key to download the file encrypted
            return await client.DownloadStreamedAsync(hash);
        }
    }

    public class FileDownloader : FileDownloaderBase
    {
        public FileDownloader(string hash, string key, string apiUrl) 
            : base(hash, key, apiUrl)
        {
        }

        protected override async Task<int> ReadStreamAsync(Stream stream, byte[] buffer, string key)
        {
            int read = await stream.ReadAsync(buffer, 0, buffer.Length);

            // Close stream after download is finished and allow caching of the file
            if (read == 0)
            {
                stream.Dispose();
            }

            return read;
        }

        protected override async Task<Stream> DownloadStreamedAsync(StorjApiClient client, string hash, string key)
        {
            return await client.DownloadStreamedAsync(hash, key);
        }
    }

    public abstract class FileDownloaderBase
    {
        private readonly string hash;
        private readonly string key;
        private readonly string apiUrl;
        private readonly string lockTask = Guid.NewGuid().ToString();
        private Task<Stream> downloadTask = null;

        public string Id { get { return lockTask; } }

        protected FileDownloaderBase(string hash, string key, string apiUrl)
        {
            this.hash = hash;
            this.key = key;
            this.apiUrl = apiUrl;
        }

        public async Task<int> ReadAsync(byte[] buffer, long offset)
        {
            EnsureDownloadStarted();

            Trace.WriteLine("Type: " + downloadTask.Result.GetType());

            if (downloadTask.Result.CanSeek)
            {
                downloadTask.Result.Seek(offset, SeekOrigin.Begin);

                return await ReadStreamAsync(downloadTask.Result, buffer, key);
            }

            int read = await ReadStreamAsync(downloadTask.Result, buffer, key);
            int additionalRead = read;

            // Read until buffer is full or nothing more to read
            while (read < buffer.Length && additionalRead > 0 && downloadTask.Result.CanRead)
            {
                byte[] additionalBuffer = new byte[buffer.Length - read];

                additionalRead = await ReadStreamAsync(downloadTask.Result, additionalBuffer, key);

                Array.Copy(additionalBuffer, 0, buffer, read, additionalRead);

                read += additionalRead;
            }

            if (read == 0)
            {
                downloadTask = null;
            }

            return read;
        }

        protected abstract Task<int> ReadStreamAsync(Stream stream, byte[] buffer, string key);

        private void EnsureDownloadStarted()
        {
            lock (lockTask)
            {
                if (downloadTask == null)
                {
                    StorjApiClient client = new StorjApiClient(apiUrl);

                    downloadTask = DownloadStreamedAsync(client, hash, key);
                }
            }
        }

        protected abstract Task<Stream> DownloadStreamedAsync(StorjApiClient client, string hash, string key);
    }
}
