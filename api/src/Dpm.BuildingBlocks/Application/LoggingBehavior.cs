using MediatR;
using Microsoft.Extensions.Logging;

namespace Dpm.BuildingBlocks.Application;

public sealed partial class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        LogHandling(logger, requestName);

        var response = await next();

        if (response is Result { IsFailure: true } result)
        {
            LogFailed(logger, requestName, result.Error.Code);
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {RequestName}")]
    private static partial void LogHandling(ILogger logger, string requestName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{RequestName} failed with {ErrorCode}")]
    private static partial void LogFailed(ILogger logger, string requestName, string errorCode);
}
