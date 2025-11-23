using Domain.Routes;
using Domain.Routes.Contexts;
using Domain.Routes.Services;

namespace Infrastructure.Routes;

public class RouteValidationService(IRouteRepository routeRepository) : IRouteValidationService
{
    private readonly IRouteRepository _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));

    public async Task<ValidationResult> ValidateRouteAsync(Domain.Routes.Route route)
    {
        if (route == null)
        {
            return ValidationResult.Failure(
                "Route cannot be null",
                Shared.ErrorCode.RouteNotFound);
        }

        var result = ValidationResult.Success();

        var nameValidation = await ValidateRouteNameAsync(route.Name, route.Id);
        result = result.Combine(nameValidation);

        return result;
    }

    public async Task<ValidationResult> ValidateRouteConflictAsync(Domain.Routes.Route newRoute)
    {
        if (newRoute == null)
        {
            return ValidationResult.Failure(
                "Route cannot be null",
                Shared.ErrorCode.RouteNotFound);
        }

        var existingRoutes = await _routeRepository.GetAllAsync(r =>
            r.IsEnabled && r.Id != newRoute.Id);

        var conflictingRoutes = existingRoutes.Where(existing =>
            HasRouteConflict(existing, newRoute)).ToList();

        if (conflictingRoutes.Any())
        {
            var conflictMessages = conflictingRoutes.Select(r =>
                $"Conflicts with route '{r.Name}' (Path: {r.Path}, Methods: {r.HttpMethods})");

            return ValidationResult.Failure(
                $"Route conflicts detected: {string.Join("; ", conflictMessages)}",
                Shared.ErrorCode.RouteConflict);
        }

        return ValidationResult.Success();
    }

    public async Task<ValidationResult> ValidateRouteNameAsync(string name, int? excludeRouteId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidationResult.Failure(
                "Route name cannot be empty", 
                Shared.ErrorCode.RouteNameEmpty, 
                nameof(name));
        }

        var existingRoute = await _routeRepository.GetRouteByNameAsync(name);
        if (existingRoute != null && existingRoute.Id != excludeRouteId)
        {
            return ValidationResult.Failure(
                $"Route name '{name}' already exists", 
                Shared.ErrorCode.RouteNameDuplicate, 
                nameof(name));
        }

        return ValidationResult.Success();
    }


    public async Task<List<ValidationResult>> ValidateRoutesAsync(List<Domain.Routes.Route> routes)
    {
       var results = new List<ValidationResult>();

        foreach (var route in routes)
        {
            var validation = await ValidateRouteAsync(route);
            var conflictValidation = await ValidateRouteConflictAsync(route);
            
            var combinedResult = validation.Combine(conflictValidation);
            results.Add(combinedResult);
        }

        return results;
    }
    
     private bool HasRouteConflict(Domain.Routes.Route existing, Domain.Routes.Route newRoute)
    {
        if ((existing.HttpMethods & newRoute.HttpMethods) ==Domain.Routes.HttpMethod.None)
            return false;

        return PathsConflict(existing, newRoute);
    }

    private bool PathsConflict(Domain.Routes.Route route1, Domain.Routes.Route route2)
    {
        if (route1.MatchType == RouteMatchType.Exact && route2.MatchType == RouteMatchType.Exact)
        {
            return string.Equals(route1.Path, route2.Path, StringComparison.OrdinalIgnoreCase);
        }

        if (route1.MatchType == RouteMatchType.Prefix || route2.MatchType == RouteMatchType.Prefix)
        {
            return route1.Path.StartsWith(route2.Path, StringComparison.OrdinalIgnoreCase) ||
                   route2.Path.StartsWith(route1.Path, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
