using System;

namespace NexIM.Server.Tools;

public interface ICryptoProvider
{
    void RandomFill(Span<byte> data);
}
