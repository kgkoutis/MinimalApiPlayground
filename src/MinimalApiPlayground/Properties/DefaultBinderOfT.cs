//using System.Collections.Concurrent;
//using System.Linq.Expressions;
using System.Reflection;

internal static class DefaultBinder<TValue>
{
    private static object _itemsKey = new();
    private static readonly RequestDelegate _defaultRequestDelegate = RequestDelegateFactory.Create(DefaultValueDelegate).RequestDelegate;
    //private static readonly ConcurrentDictionary<(Type, ParameterInfo), RequestDelegate> _delegateCache = new();

    public static async Task<(TValue?, int)> GetValueAsync(HttpContext httpContext, ParameterInfo parameter)
    {
        //var requestDelegate = _delegateCache.GetOrAdd((typeof(TValue), parameter), CreateRequestDelegate);
        
        var originalStatusCode = httpContext.Response.StatusCode;

        //await requestDelegate(httpContext);
        await _defaultRequestDelegate(httpContext);

        if (originalStatusCode != httpContext.Response.StatusCode)
        {
            // Default binder ran and detected an issue
            httpContext.Response.StatusCode = originalStatusCode;
            return (default(TValue?), httpContext.Response.StatusCode);
        }

        return ((TValue?)httpContext.Items[_itemsKey], StatusCodes.Status200OK);
    }

    // BUG: This doesn't work right now as the parameters for dynamic methods can't have names!
    //      RequestDelegateFactory.Create throws if the delegate passed to it has unnamed parameters.
    //private static RequestDelegate CreateRequestDelegate((Type, ParameterInfo) key)
    //{
    //    var valueParam = Expression.Parameter(key.Item1, key.Item2.Name);
    //    var contextParam = Expression.Parameter(typeof(HttpContext), "httpContext");
    //    var itemsProp = Expression.Property(contextParam, "Items");
    //    var indexer = typeof(IDictionary<object, object>).GetProperty("Item");
    //    var itemsDictIndex = Expression.Property(itemsProp, indexer!, Expression.Constant(_itemsKey));
    //    var returnTarget = Expression.Label(typeof(IResult));

    //    var compiled = Expression.Lambda<Func<TValue, HttpContext, IResult>>(
    //        Expression.Block(
    //            Expression.Assign(itemsDictIndex, valueParam),
    //            Expression.Label(returnTarget, Expression.Constant(FakeResult.Instance))
    //        ),
    //        new[] { valueParam, contextParam})
    //        .Compile();

    //    return RequestDelegateFactory.Create(compiled).RequestDelegate;
    //}

    private static IResult DefaultValueDelegate(TValue value, HttpContext httpContext)
    {
        httpContext.Items[_itemsKey] = value;
        
        return FakeResult.Instance;
    }

    private class FakeResult : IResult
    {
        public static FakeResult Instance { get; } = new FakeResult();

        public Task ExecuteAsync(HttpContext httpContext)
        {
            return Task.CompletedTask;
        }
    }
}
