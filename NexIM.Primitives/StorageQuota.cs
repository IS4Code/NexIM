using System;
using System.Threading;

namespace NexIM.Primitives;

public abstract class StorageQuota
{
    static readonly AsyncLocal<StorageQuota?> local = new();

    public static StorageQuota Local {
        get => local.Value ?? throw new InvalidOperationException("No storage quota context has been set yet.");
        set => local.Value = value;
    }

    public static readonly StorageQuota Empty = new EmptyQuota();

    public abstract void RequestBytes(long count);
    public abstract void ReleaseBytes(long count);
    public abstract void RequestFiles(int count);
    public abstract void ReleaseFiles(int count);

    sealed class EmptyQuota : StorageQuota
    {
        public override void ReleaseBytes(long count)
        {

        }

        public override void RequestBytes(long count)
        {

        }

        public override void RequestFiles(int count)
        {

        }

        public override void ReleaseFiles(int count)
        {

        }
    }
}
