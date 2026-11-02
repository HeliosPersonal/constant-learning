using System.Net;

namespace ConstantLearning.Platform.Metrics;

public static class MetricsEndpoints
{
    public static void MapLocalMetrics(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        app.MapGet("/metrics", (HttpContext context, TrainerMetrics metrics) =>
        {
            var address = context.Connection.RemoteIpAddress;
            if (address is null || !IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address))
            {
                return Results.NotFound() as IResult;
            }

            context.Response.Headers.CacheControl = "no-store";
            return Results.Text(metrics.ExportPrometheus(), "text/plain; version=0.0.4; charset=utf-8");
        }).AllowAnonymous().ExcludeFromDescription();
    }
}
