using System.Text;
using System.Text.Json;
using FleetManager.Helpers;

namespace FleetManager;

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
                return result;
        }

        return result;
    }
}
