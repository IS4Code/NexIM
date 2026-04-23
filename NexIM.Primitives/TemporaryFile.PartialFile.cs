using System;
using System.IO;
using System.Threading.Tasks;
using static NexIM.Primitives.TemporaryArray<byte>;

namespace NexIM.Primitives;

partial class TemporaryFile
{
    sealed class PartialFile(StorageQuota quota, PartialFile? other = null) : TemporaryFile(other)
    {
        protected override string CreatePath()
        {
            quota.RequestFiles(1);
            return Path.Combine(Path.GetTempPath(), $"{nameof(NexIM)}_{Guid.NewGuid():N}");
        }

        public override TemporaryFile MoveFrom()
        {
            return new PartialFile(quota, this);
        }

        public void ReadFrom<TArgs>(SynchronousReader<TArgs> reader, TArgs args)
        {
            var buffer = pool.Rent(bufferSize);
            try
            {
                using var stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.Delete, buffer.Length, FileOptions.SequentialScan);
                var segment = new ArraySegment<byte>(buffer, 0, buffer.Length);
                int read;
                while((read = reader(segment, args)) > 0)
                {
                    quota.RequestBytes(read);
                    stream.Write(buffer, 0, read);
                }
            }
            finally
            {
                pool.Return(buffer, true);
            }
        }

        public async ValueTask ReadFromAsync<TArgs>(AsynchronousReader<TArgs> reader, TArgs args)
        {
            var buffer = pool.Rent(bufferSize);
            try
            {
                using var stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.Delete, buffer.Length, FileOptions.SequentialScan | FileOptions.Asynchronous);
                var segment = new ArraySegment<byte>(buffer, 0, buffer.Length);
                int read;
                while((read = await reader(segment, args)) > 0)
                {
                    quota.RequestBytes(read);
                    await stream.WriteAsync(buffer, 0, read);
                }
            }
            finally
            {
                pool.Return(buffer, true);
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if(File.Exists(FilePath))
                {
                    // Deleted on close
                    using var stream = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Delete, bufferSize, FileOptions.DeleteOnClose);
                    var length = stream.Length;
                    stream.SetLength(0);
                    quota.ReleaseBytes(length);
                }
                quota.ReleaseFiles(1);
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
