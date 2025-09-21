using Domain.Routes.Contexts;

namespace Domain.Routes.Services
{
    public interface IRouteValidationService
    {
        Task<ValidationResult> ValidateRouteAsync(Route route);
        Task<ValidationResult> ValidateRouteConflictAsync(Route newRoute);
        Task<ValidationResult> ValidateRouteNameAsync(string name, int? excludeRouteId = null);
        Task<List<ValidationResult>> ValidateRoutesAsync(List<Route> routes);
    }
}