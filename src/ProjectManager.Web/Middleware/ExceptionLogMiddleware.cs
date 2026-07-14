using System.Security.Claims;
using Microsoft.Extensions.Logging;
using ProjectManager.Web.Services.Operations;

namespace ProjectManager.Web.Middleware;

public sealed class ExceptionLogMiddleware(
    RequestDelegate next,
    ILogger<ExceptionLogMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ExceptionLogStore logs)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            try
            {
                await logs.WriteAsync(
                    exception,
                    context.Request.Path,
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                    context.TraceIdentifier,
                    CancellationToken.None);
            }
            catch (Exception logException)
            {
                logger.LogError(
                    logException,
                    "Unable to persist exception log for trace {TraceIdentifier}.",
                    context.TraceIdentifier);
            }

            throw;
        }
    }
}
