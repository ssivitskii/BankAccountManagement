using Banking.Application;
using Banking.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int status, string title) = exception switch
        {
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Authentication failed"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Access denied"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ConflictException or InsufficientFundsException => (StatusCodes.Status409Conflict, "Conflict"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error"),
        };
        if (status >= 500)
            _logger.LogError(exception, "Unhandled API exception");
        else
            _logger.LogInformation("Request failed with {StatusCode}: {Reason}", status, exception.Message);
        httpContext.Response.StatusCode = status;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status >= 500 ? "An unexpected error occurred." : exception.Message,
                Instance = httpContext.Request.Path,
            },
        }).ConfigureAwait(false);
    }
}
