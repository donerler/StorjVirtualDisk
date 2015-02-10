using StorjClient;
using StorjClient.Data;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace StorjVirtualDisk
{
    public class FileUploader
    {
        private readonly string fileName;
        private readonly string apiUrl;
        private readonly string lockTask = Guid.NewGuid().ToString();
        private bool isFinal = false;
        private long length = 0;
        private ConcurrentQueue<byte[]> data = new ConcurrentQueue<byte[]>();
        private Task<UploadedFile> uploadTask = null;

        public string FileName { get { return fileName; } }
        public long Size { get; set; }
        public string Id { get { return lockTask; } }

        public FileUploader(string fileName, string apiUrl)
        {
            this.fileName = fileName;
            this.apiUrl = apiUrl;
        }

        private async Task UploadAsync(Stream outputStream, HttpContent httpContent, TransportContext transportContext)
        {
            try
            {
                while (!isFinal || data.Any())
                {
                    byte[] buffer;

                    if (data.TryDequeue(out buffer))
                    {
                        await outputStream.WriteAsync(buffer, 0, buffer.Length);

                        length += buffer.Length;
                    }
                }
            }
            finally
            {
                outputStream.Close();
            }
        }

        private Task Upload(Stream outputStream)
        {
            return 
            Task.Factory.StartNew(() =>
            {
                try
                {
                    while (!isFinal || data.Any())
                    {
                        byte[] buffer;

                        if (data.TryDequeue(out buffer))
                        {
                            outputStream.Write(buffer, 0, buffer.Length);

                            length += buffer.Length;

                            Trace.WriteLine(string.Format("Written {0} ({1}) of {2} ({3:0} blocks of {4:0})", buffer.Length, length, Size, length / buffer.Length, Size / buffer.Length));
                        }
                    }
                }
                finally
                {
                }
            });
        }

        public async Task WriteAsync(byte[] buffer)
        {
            EnsureUploadStarted();

            long targetLength = length + buffer.Length;

            data.Enqueue(buffer);

            while (length < targetLength)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }

        public bool IsFinished()
        {
            return Size == length;
        }

        private void EnsureUploadStarted()
        {
            lock (lockTask)
            {
                if (uploadTask == null)
                {
                    StorjApiClient client = new StorjApiClient(apiUrl);

                    if (Size > 0)
                    {
                        uploadTask = client.UploadStreamedAsync(Upload, fileName, Size);
                    }
                    else
                    {
                        uploadTask = client.UploadAsync(UploadAsync, fileName);
                    }
                }
            }
        }

        public async Task<UploadedFile> Close()
        {
            lock (lockTask)
            {
                if (uploadTask == null || isFinal)
                {
                    return null;
                }

                isFinal = true;
            }

            return await uploadTask;
        }
    }
}
