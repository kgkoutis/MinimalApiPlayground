using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Diagnostics;

/// <summary>
/// Formats <see cref="DeveloperExceptionPageMiddleware"/> exceptions as JSON Problem Details if the client indicates it accepts JSON.
/// </summary>
public class ProblemDetailsDeveloperPageExceptionFilter : IDeveloperPageExceptionFilter
{
    private static readonly MediaTypeHeaderValue _jsonMediaType = new MediaTypeHeaderValue("application/json");

    public async Task HandleExceptionAsync(ErrorContext errorContext, Func<ErrorContext, Task> next)
    {
        var headers = errorContext.HttpContext.Request.GetTypedHeaders();
        var acceptHeader = headers.Accept;
        var ex = errorContext.Exception;
        var httpContext = errorContext.HttpContext;

        if (acceptHeader?.Any(h => _jsonMediaType.IsSubsetOf(h)) == true)
        {
            var problemDetails = new ProblemDetails
            {
                Title = $"An unhandled exception occurred while processing the request",
                Detail = $"{ex.GetType().Name}: {ex.Message}",
                Status = ex switch
                {
                    BadHttpRequestException bhre => bhre.StatusCode,
                    _ => StatusCodes.Status500InternalServerError
                }
            };
            problemDetails.Extensions.Add("exception", ex.GetType().FullName);
            problemDetails.Extensions.Add("stack", ex.StackTrace);
            problemDetails.Extensions.Add("headers", httpContext.Request.Headers.ToDictionary(kvp => kvp.Key, kvp => (string)kvp.Value));
            problemDetails.Extensions.Add("routeValues", httpContext.GetRouteData().Values);
            problemDetails.Extensions.Add("query", httpContext.Request.Query);
            var endpoint = httpContext.GetEndpoint();
            if (endpoint != null)
            {
                var routeEndpoint = endpoint as RouteEndpoint;
                var httpMethods = endpoint?.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
                problemDetails.Extensions.Add("endpoint", new
                {
                    endpoint?.DisplayName,
                    routePattern = routeEndpoint?.RoutePattern.RawText,
                    routeOrder = routeEndpoint?.Order,
                    httpMethods = httpMethods != null ? string.Join(", ", httpMethods) : ""
                });
            }
            var requestId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
            problemDetails.Extensions.Add("requestId", requestId);

            await Results.Json(problemDetails, null, "application/problem+json", problemDetails.Status)
                .ExecuteAsync(httpContext);

            await Results.Extensions.Problem(problemDetails).ExecuteAsync(httpContext);
        }
        else
        {
            await next(errorContext);
        }
    }
}

public static class ProblemDetailsDeveloperPageExtensions
{
    /// <summary>
    /// Adds a <see cref="IDeveloperPageExceptionFilter"/> that formats all exceptions as JSON Problem Details to clients
    /// that indicate they support JSON via the Accepts header.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/></param>
    /// <returns>The <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddProblemDetailsDeveloperPageExceptionFilter(this IServiceCollection services) =>
        services.AddSingleton<IDeveloperPageExceptionFilter, ProblemDetailsDeveloperPageExceptionFilter>();
}
