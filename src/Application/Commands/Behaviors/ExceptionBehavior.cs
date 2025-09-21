using Droplet.Utility.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Commands.Behaviors
{
    public class ExceptionBehavior<TRequest, TResponse>(ILogger<ExceptionBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly ILogger _logger = logger;
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            try
            {
                return await next();
            }
            catch (InvalidCommandException ex)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(ex, "InvalidCommandException");
                }
                throw;
            }
            catch (FriendlyException ex)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(ex, "BusinessValidationException");
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR Handling for {CommandName} ({@Command})", request.GetType(), request);
                throw;
            }
        }
    }
}
