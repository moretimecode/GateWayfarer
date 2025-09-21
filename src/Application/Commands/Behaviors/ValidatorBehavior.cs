using Droplet.Utility.Exceptions;
using FluentValidation;
using MediatR;
using Shared;
using System.Text;

namespace Application.Commands.Behaviors
{
    public class ValidatorBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var errors = validators.Select(x => x.Validate(request, options => options.IncludeAllRuleSets()))
                .SelectMany(x => x.Errors)
                .Where(error => error != null)
                .ToList();

            if (errors.Count != 0)
            {
                var messageBuilder = new StringBuilder("Invalid arguments:");
                foreach (var error in errors)
                {
                    messageBuilder.AppendLine(error.ErrorMessage);
                }
                throw new FriendlyException(messageBuilder.ToString(), ErrorCode.InvalidArgument);
            }
            return next();
        }
    }
}
