using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace ConstantLearning.Platform.Metrics;

public sealed class ServerTimingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TrainerMetrics metrics)
    {
        var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern;
        var route = pattern is null ? null : string.Join('/', pattern.PathSegments.Select(segment => segment.Parts switch
        {
            [RoutePatternLiteralPart literal] => literal.Content,
            [RoutePatternParameterPart] => "{}",
            _ => string.Empty
        }));
        var isAnswer = HttpMethods.IsPost(context.Request.Method)
                       && string.Equals(route, "api/v1/sessions/{}/items/{}/answer", StringComparison.OrdinalIgnoreCase);
        var isSearch = HttpMethods.IsGet(context.Request.Method)
                       && string.Equals(route, "api/v1/dictionary/search", StringComparison.OrdinalIgnoreCase);
        if (!isAnswer && !isSearch)
        {
            await next(context);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            if (isAnswer)
            {
                metrics.RecordAnswer(elapsed);
            }
            else
            {
                metrics.RecordDictionarySearch(elapsed);
            }
        }
    }
}
