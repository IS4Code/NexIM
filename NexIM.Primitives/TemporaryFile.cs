using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static NexIM.Primitives.TemporaryArray<byte>;

namespace NexIM.Primitives;

/// <summary>
/// Represents a file reference with limited lifetime.
/// </summary>
public abstract partial class TemporaryFile : IDisposable
{
    static readonly ArrayPool<byte> pool = ArrayPool<byte>.Shared;

    const int bufferSize = 4096;

    readonly Func<string> pathInitializer;

    string? _path;
    string? _originalPath;
    
    public string FilePath => LazyInitializer.EnsureInitialized(ref _path, pathInitializer)!;

    protected TemporaryFile()
    {
        pathInitializer = CreatePath;
    }

    protected TemporaryFile(TemporaryFile? source)
    {
        if(source == null || source == this)
        {
            // Nothing to move from
            pathInitializer = CreatePath;
            return;
        }

        // Ensure the path is initialized, and pull it out
        _ = source.FilePath;
        _originalPath = Interlocked.Exchange(ref source._path, "");

        // The path might still need to be adjusted based on the concrete class
        pathInitializer = UpdatePath;
    }

    protected abstract string CreatePath();

    private string UpdatePath()
    {
        var oldPath = Interlocked.Exchange(ref _originalPath, "")!;
        if(!File.Exists(oldPath))
        {
            // Moved from a dead instance
            return oldPath;
        }

        var newPath = CreatePath();
        if(File.Exists(newPath))
        {
            // Assume the files are equivalent; delete the old one
            File.Delete(oldPath);
        }
        else
        {
            File.Move(oldPath, newPath);
        }
        return newPath;
    }

    public abstract TemporaryFile MoveFrom();

    public void WriteTo<TArgs>(SynchronousWriter<TArgs> writer, TArgs args)
    {
        var buffer = pool.Rent(bufferSize);
        try
        {
            using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, FileOptions.SequentialScan);
            int read;
            while((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                writer(new(buffer, 0, read), args);
            }
        }
        finally
        {
            pool.Return(buffer, true);
        }
    }

    public async ValueTask WriteToAsync<TArgs>(AsynchronousWriter<TArgs> writer, TArgs args)
    {
        var buffer = pool.Rent(bufferSize);
        try
        {
            using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, FileOptions.SequentialScan | FileOptions.Asynchronous);
            int read;
            while((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await writer(new(buffer, 0, read), args);
            }
        }
        finally
        {
            pool.Return(buffer, true);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        // Ownership determined by deriving classes
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
        var file = new PartialFile(quota);
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
        var file = new PartialFile(quota);
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
}
