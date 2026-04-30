using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace NexIM.Primitives;

public static class ReferenceRemoteExtensions
{
    public static ValueTask<TResult?> Get<TObject, TResult>(this Remote<TObject> remote, Expression<Func<TObject, TResult>> retrieveExpression, CancellationToken cancellationToken = default) where TObject : notnull where TResult : class
    {
        return remote.Get(retrieveExpression, static () => null!, cancellationToken)!;
    }
}

public static class ValueRemoteExtensions
{
    public static async ValueTask<TResult?> Get<TObject, TResult>(this Remote<TObject> remote, Expression<Func<TObject, TResult>> retrieveExpression, CancellationToken cancellationToken = default) where TObject : notnull where TResult : struct
    {
        bool isDefault = false;
        var result = await remote.Get(retrieveExpression, () => {
            isDefault = true;
            return default;
        }, cancellationToken);
        if(isDefault)
        {
            return null;
        }
        return result;
    }
}
