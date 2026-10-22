using Kalon.Back.Exceptions;

namespace Kalon.Back.Middleware;

public class InsufficientSearchCreditsExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public InsufficientSearchCreditsExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (InsufficientSearchCreditsException iex)
        {
            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            await context.Response.WriteAsJsonAsync(new
            {
                error = iex.Message,
                availableCredits = iex.Available,
                requiredCredits = iex.Required,
                canPurchase = true
            });
        }
    }
}
