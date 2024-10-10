using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PlaylistPlayer.Helpers;

namespace PlaylistPlayer;

public class ETagFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var result = await next(context);

        if (context.HttpContext.Request.Method != "GET")
            return result;

        switch (result)
        {
            case IValueHttpResult valueResult
            and IStatusCodeHttpResult statusCodeResult:
            {
                var statusCode =
                    statusCodeResult.StatusCode ?? context.HttpContext.Response.StatusCode;

                if (statusCode != StatusCodes.Status200OK)
                    return result;

                var content = JsonSerializer.Serialize(valueResult.Value);

                var eTag = ETagGenerator.GetETag(
                    context.HttpContext.Request.Path.ToString(),
                    Encoding.UTF8.GetBytes(content)
                );

                if (context.HttpContext.Request.Headers.IfNoneMatch == eTag)
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status304NotModified;
                    return Results.StatusCode(StatusCodes.Status304NotModified);
                }

                context.HttpContext.Response.Headers.ETag = eTag;
                break;
            }
            case IResult:
                // For other types of IResult, we can't generate an ETag
                // So we just return the result as-is
                return result;
        }

        return result;
    }
}
