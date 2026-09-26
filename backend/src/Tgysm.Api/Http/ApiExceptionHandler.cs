using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Tgysm.Application.Common;
using Tgysm.Domain.Common;

namespace Tgysm.Api.Http;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case OperationCanceledException when context.RequestAborted.IsCancellationRequested:
                // The client went away; there is nobody to answer and nothing went wrong on the server.
                logger.LogDebug("Request {Method} {Path} was cancelled by the client.", context.Request.Method, context.Request.Path);
                break;
            case ValidationException validation:
                await ApiProblem.WriteAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, ValidationErrorMapper.ToFieldCodes(validation.Errors));
                break;
            case BadHttpRequestException:
                await ApiProblem.WriteAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed);
                break;
            case AuthenticationFailedException:
                await ApiProblem.WriteAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.InvalidCredentials);
                break;
            case NotFoundException notFound:
                await ApiProblem.WriteAsync(context, StatusCodes.Status404NotFound, notFound.Code);
                break;
            case ConcurrencyException:
                await ApiProblem.WriteAsync(context, StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict);
                break;
            case DomainException domain:
                await ApiProblem.WriteAsync(context, StatusCodes.Status409Conflict, domain.Code);
                break;
            default:
                logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
                await ApiProblem.WriteAsync(context, StatusCodes.Status500InternalServerError, ErrorCodes.Unexpected);
                break;
        }

        return true;
    }
}
