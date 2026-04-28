using System;
using System.Threading.Tasks;

namespace NexIM.Tools;

public static class Extensions
{
    public static ValueTask DisposeNotNullAsync<T>(this T? disposable) where T : class, IAsyncDisposable
    {
        if(disposable == null)
        {
            return default;
        }
        return disposable.DisposeAsync();
    }
}
