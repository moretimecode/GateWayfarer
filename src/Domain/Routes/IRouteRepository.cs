using Droplet.Data.Repositories;

namespace Domain.Routes;

public interface IRouteRepository : IRepository<Route, int>
{
    Task<Domain.Routes.Route?> GetRouteByNameAsync(string name);
    ValueTask<List<Route>> GetEnabledRoutesOrderedByPriorityAsync();
}