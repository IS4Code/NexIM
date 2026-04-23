using System;
using System.Threading.Tasks;
using NexIM.Xrd.Protocol;

namespace NexIM.Metadata;

public interface IMetadataProvider
{
    ValueTask<IMetadataDescriptor?> GetHostDescriptor(Uri uri);
}

public interface IMetadataDescriptor
{
    ValueTask Properties(Uri uri, IResourceDescriptorHandler handler);
    ValueTask Links(Uri uri, IResourceDescriptorHandler handler);
}
