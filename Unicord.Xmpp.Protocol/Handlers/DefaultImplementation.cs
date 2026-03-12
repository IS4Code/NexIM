using System;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Protocol.Handlers;

internal abstract class DefaultImplementation<T>
{
    private protected static readonly Task<T> DefaultTask = Task.FromException<T>(DefaultImplementation.Exception);

    public static ValueTask<T> ValueTask => new(DefaultTask);
}

internal abstract class DefaultImplementation : DefaultImplementation<object>
{
    public static readonly Exception Exception;

    public static new ValueTask ValueTask => new(DefaultTask);

    static DefaultImplementation()
    {
        try
        {
            throw new InvalidOperationException("The default implementation cannot be directly awaited.");
        }
        catch(Exception e)
        {
            Exception = e;
        }
    }
}
