using System.Diagnostics;
using System.Net.Mime;
using System.Text;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Http;

static class ResultsExtensions
{
    public static IResult Problem(this IResultExtensions resultExtensions, string? detail = null, string? instance = null, int? statusCode = null, string? title = null, string? type = null, Dictionary<string, string>? extensions = null)
    {
        var problemDetails = new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = statusCode,
            Instance = instance,
            Type = type
        };
        if (extensions != null)
        {
            foreach (var extension in extensions)
            {
                problemDetails.Extensions.Add(extension.Key, extension.Value);
            }
        }

        return new ProblemResult(problemDetails);
    }

    public static IResult Problem(this IResultExtensions resultExtensions, ProblemDetails problemDetails)
    {
        return new ProblemResult(problemDetails);
    }

    public static IResult CreatedWithContentType<T>(this IResultExtensions resultExtensions, T responseBody, string contentType)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions, nameof(resultExtensions));

        return new CreatedWithContentTypeResult<T>(responseBody, contentType);
    }

    public static IResult Html(this IResultExtensions resultExtensions, string html)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions, nameof(resultExtensions));

        return new HtmlResult(html);
    }

    class ProblemResult : IResult
    {
        private readonly ProblemDetails _problemDetails;

        public ProblemResult(ProblemDetails problemDetails)
        {
            _problemDetails = problemDetails;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            _problemDetails.Status ??= _problemDetails is HttpValidationProblemDetails ?
                StatusCodes.Status400BadRequest :
                StatusCodes.Status500InternalServerError;

            if (ProblemDetailsDefaults.Defaults.TryGetValue(_problemDetails.Status.Value, out var defaults))
            {
                _problemDetails.Title ??= defaults.Title;
                _problemDetails.Type ??= defaults.Type;
            }

            if (!_problemDetails.Extensions.ContainsKey("requestId"))
            {
                _problemDetails.Extensions.Add("requestId", Activity.Current?.Id ?? httpContext.TraceIdentifier);
            }

            httpContext.Response.StatusCode = _problemDetails.Status.Value;
            await httpContext.Response.WriteAsJsonAsync(_problemDetails, _problemDetails.GetType(), options: null, contentType: "application/problem+json");
        }
    }

    class CreatedWithContentTypeResult<T> : IResult
    {
        private readonly T _responseBody;
        private readonly string _contentType;

        public CreatedWithContentTypeResult(T responseBody, string contentType)
        {
            _responseBody = responseBody;
            _contentType = contentType;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            // Likely should honor Accpets header, etc.
            httpContext.Response.StatusCode = StatusCodes.Status201Created;
            httpContext.Response.ContentType = _contentType;

            switch (_contentType)
            {
                case "application/xml":
                    // This is terrible code, don't do this
                    var xml = new XmlSerializer(typeof(T));
                    using (var ms = new MemoryStream())
                    {
                        xml.Serialize(ms, _responseBody);
                        ms.Seek(0, SeekOrigin.Begin);
                        await ms.CopyToAsync(httpContext.Response.Body);
                    }
                    break;

                case "application/json":
                default:
                    await httpContext.Response.WriteAsJsonAsync(_responseBody);
                    break;
            }
        }
    }

    class HtmlResult : IResult
    {
        private readonly string _html;

        public HtmlResult(string html)
        {
            _html = html;
        }

        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = MediaTypeNames.Text.Html;
            httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(_html);
            return httpContext.Response.WriteAsync(_html);
        }
    }
}
