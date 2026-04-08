using System;
using System.Threading.Tasks;
using Unicord.Xrd.Protocol;

namespace Unicord.Metadata;

public interface IMetadataProvider
{
    ValueTask<IMetadataDescriptor?> GetHostDescriptor(Uri uri);
}

public interface IMetadataDescriptor
{
    ValueTask Properties(Uri uri, IResourceDescriptorHandler handler);
    ValueTask Links(Uri uri, IResourceDescriptorHandler handler);
}
