using System;
using System.Threading.Tasks;

namespace NexIM.Primitives.Xml.Handlers;

public abstract class DefaultImplementation<T>
{
    private protected static readonly Task<T> DefaultTask = Task.FromException<T>(DefaultImplementation.Exception);

    public static ValueTask<T> ValueTask => new(DefaultTask);

    internal DefaultImplementation()
    {

    }
}

public abstract class DefaultImplementation : DefaultImplementation<object>
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

    internal DefaultImplementation()
    {

    }
}
