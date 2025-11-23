using Domain.Routes;
using Domain.Routes.Configuration;
using Droplet.Data.EntityFrameworkCore;
using Droplet.Data.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Routes
{
    public class RouteRepository(IUnitOfWork unitOfWork, IOptionsMonitor<Domain.Routes.Configuration.RouteOptions> routeOption) : EntityFrameworkCoreRepository<Domain.Routes.Route, int>(unitOfWork), IRouteRepository
    {
        private readonly Domain.Routes.Configuration.RouteOptions _routes = routeOption.CurrentValue;
        public async Task<Domain.Routes.Route?> GetRouteByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return await Table.FirstOrDefaultAsync(r => r.Name == name);
        }


        public async Task<Dictionary<string, bool>> CheckRoutesExistAsync(List<string> routeNames)
        {
            var result = new Dictionary<string, bool>();

            if (routeNames == null || !routeNames.Any())
                return result;

            var existingRoutes = await Table.Where(r =>
                routeNames.Contains(r.Name)).ToListAsync();

            var existingNames = existingRoutes.Select(r => r.Name).ToHashSet();

            foreach (var routeName in routeNames)
            {
                result[routeName] = existingNames.Contains(routeName);
            }

            return result;
        }

        public async Task<bool> RouteExistsAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return await Table.AnyAsync(r => r.Name == name);
        }

        public async Task<bool> RouteExistsAsync(int id)
        {
            if (id <= 0)
                return false;

            return await Table.AnyAsync(x => x.Id == id);

        }

        public ValueTask<List<Domain.Routes.Route>> GetEnabledRoutesOrderedByPriorityAsync()
        {
            var result = _routes.Routes.Where(r => r.IsEnabled)
               .OrderByDescending(r => r.Priority)
               .ThenBy(r => r.Name)
               .Select(x => new Domain.Routes.Route(x.Name, x.Path, x.ServiceCode, x.HttpMethods, x.Priority, x.IsEnabled, x.MatchType, x.LoadBalanceType))
               .ToList();
            return ValueTask.FromResult(result);
        }
    }
}