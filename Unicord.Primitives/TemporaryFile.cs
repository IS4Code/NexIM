using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using static Unicord.Primitives.TemporaryArray<byte>;

namespace Unicord.Primitives;

public abstract class TemporaryFile : IDisposable
{
    readonly string path;

    protected byte[] Buffer { get; }
    const int bufferSize = 4096;

    protected TemporaryFile(string path)
    {
        this.path = path;
        Buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
    }

    public void WriteTo<TArgs>(SynchronousWriter<TArgs> writer, TArgs args)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
        int read;
        while((read = stream.Read(Buffer, 0, Buffer.Length)) > 0)
        {
            writer(new(Buffer, 0, read), args);
        }
    }

    public async ValueTask WriteToAsync<TArgs>(AsynchronousWriter<TArgs> writer, TArgs args)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
        int read;
        while((read = await stream.ReadAsync(Buffer, 0, Buffer.Length)) > 0)
        {
            await writer(new(Buffer, 0, read), args);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if(disposing)
        {
            ArrayPool<byte>.Shared.Return(Buffer, true);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~TemporaryFile()
    {
        Dispose(false);
    }

    public static TemporaryFile ReadFrom<TArgs>(StorageQuota quota, SynchronousReader<TArgs> reader, TArgs args)
    {
        var file = new QuotaFile(quota);
        try
        {
            file.ReadFrom(reader, args);
            return file;
        }
        catch when(Dispose())
        {
            throw;
        }

        bool Dispose()
        {
            file.Dispose();
            return false;
        }
    }

    public static async ValueTask<TemporaryFile> ReadFromAsync<TArgs>(StorageQuota quota, AsynchronousReader<TArgs> reader, TArgs args)
    {
        var file = new QuotaFile(quota);
        try
        {
            await file.ReadFromAsync(reader, args);
            return file;
        }
        catch when(Dispose())
        {
            throw;
        }

        bool Dispose()
        {
            file.Dispose();
            return false;
        }
    }

    sealed class QuotaFile : TemporaryFile
    {
        readonly StorageQuota quota;

        public QuotaFile(StorageQuota quota) : base(CreateTemporaryFile(quota))
        {
            this.quota = quota;
        }

        static string CreateTemporaryFile(StorageQuota quota)
        {
            quota.RequestFiles(1);
            return Path.GetTempFileName();
        }

        public void ReadFrom<TArgs>(SynchronousReader<TArgs> reader, TArgs args)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Delete, bufferSize, FileOptions.SequentialScan);
            var segment = new ArraySegment<byte>(Buffer, 0, Buffer.Length);
            int read;
            while((read = reader(segment, args)) > 0)
            {
                quota.RequestBytes(read);
                stream.Write(Buffer, 0, read);
            }
        }

        public async ValueTask ReadFromAsync<TArgs>(AsynchronousReader<TArgs> reader, TArgs args)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Delete, bufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
            var segment = new ArraySegment<byte>(Buffer, 0, Buffer.Length);
            int read;
            while((read = await reader(segment, args)) > 0)
            {
                quota.RequestBytes(read);
                await stream.WriteAsync(Buffer, 0, read);
            }
        }

        private void Truncate()
        {
            using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Delete);
            var length = stream.Length;
            stream.SetLength(0);
            quota.ReleaseBytes(length);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                Truncate();
                File.Delete(path);
                quota.ReleaseFiles(1);
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
