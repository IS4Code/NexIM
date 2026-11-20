using System;
using NexIM.Server.Tools;

namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class ScramTests : TestHelper
{
    const string user = "scram";

    static readonly byte[] salt = CreateSalt();

    // 18 bytes
    static readonly byte[] serverNonce = "nexim-server-nonce"u8.ToArray();

    static byte[] CreateSalt()
    {
        var bytes = new byte[32];
        for(int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)i;
        }
        return bytes;
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        ServerInitialize(testContext);

        var server = receiver.Server;
        server.CryptoProvider = new CryptoProvider(salt);
        RegisterAccount(server, user, user, "scram@example.org").AsTask().GetAwaiter().GetResult();
        server.CryptoProvider = new CryptoProvider(serverNonce);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        ServerCleanup();
    }

    [TestMethod]
    public void ScramSha256Authenticates()
    {
        var client = CreateClient("ScramPrologue1", "ScramPrologue2");
        client.FinishReceive();
    }

    [TestMethod]
    public void ScramSha256WrongProofIsRejected()
    {
        var client = CreateClient("ScramPrologue1Retry", "ScramPrologue2");
        client.FinishReceive();
    }

    sealed class CryptoProvider(byte[] random) : ICryptoProvider
    {
        public void RandomFill(Span<byte> data)
        {
            random.CopyTo(data);
        }
    }
}
