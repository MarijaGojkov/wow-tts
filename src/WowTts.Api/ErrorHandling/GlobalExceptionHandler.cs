using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WowTts.Application.Exceptions;

namespace WowTts.Api.ErrorHandling;

// Turns exceptions from the inner layers into ProblemDetails responses, so controllers don't need try/catch.
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int status, string title) = exception switch
        {
            InvalidRequestException => (StatusCodes.Status400BadRequest, "Invalid request"),
            SpeechSynthesisException => (StatusCodes.Status502BadGateway, "Azure Speech failed"),
            _ => (StatusCodes.Status500InternalServerError, "Something went wrong")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");
        else
            logger.LogWarning("{Title}: {Message}", title, exception.Message);

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Unexpected errors keep their details in the log, not in the response.
                Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message
            }
        });
    }
}
