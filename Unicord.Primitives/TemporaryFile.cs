using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using static Unicord.Primitives.TemporaryArray<byte>;

namespace Unicord.Primitives;

public abstract class TemporaryFile : IDisposable
{
    readonly string path;
    readonly FileStream stream;

    protected byte[] Buffer { get; }
    const int bufferSize = 4096;

    public int Length => (int)stream.Length;

    protected TemporaryFile(string path, FileStream stream)
    {
        this.path = path;
        this.stream = stream;
        Buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
    }

    public void WriteTo<TArgs>(SynchronousWriter<TArgs> writer, TArgs args)
    {
        stream.Position = 0;
        int read;
        while((read = stream.Read(Buffer, 0, Buffer.Length)) > 0)
        {
            writer(new(Buffer, 0, read), args);
        }
    }

    public async ValueTask WriteToAsync<TArgs>(AsynchronousWriter<TArgs> writer, TArgs args)
    {
        stream.Position = 0;
        int read;
        while((read = await stream.ReadAsync(Buffer, 0, Buffer.Length)) > 0)
        {
            await writer(new(Buffer, 0, read), args);
        }
    }

    public FileStream OpenStream()
    {
        return new(path, FileMode.Open, FileAccess.Read, (FileShare)(-1));
    }

    protected virtual void Dispose(bool disposing)
    {
        if(disposing)
        {
            stream.Dispose();
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

        public QuotaFile(StorageQuota quota) : base(CreateTemporaryFile(quota, out var stream), stream)
        {
            this.quota = quota;
        }

        static string CreateTemporaryFile(StorageQuota quota, out FileStream stream)
        {
            quota.RequestFiles(1);
            var path = Path.GetTempFileName();
            stream = new(path, FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete, bufferSize, FileOptions.Asynchronous | FileOptions.DeleteOnClose | FileOptions.SequentialScan);
            return path;
        }

        public void ReadFrom<TArgs>(SynchronousReader<TArgs> reader, TArgs args)
        {
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
            int length = Length;
            stream.SetLength(0);
            quota.ReleaseBytes(length);
        }

        protected override void Dispose(bool disposing)
        {
            Truncate();
            base.Dispose(disposing);
            quota.ReleaseFiles(1);
        }
    }
}
