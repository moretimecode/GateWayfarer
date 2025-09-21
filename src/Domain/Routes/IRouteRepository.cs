using Droplet.Data.Repositories;

namespace Domain.Routes;

public interface IRouteRepository : IRepository<Route, int>
{
    Task<Route?> GetRouteByNameAsync(string name);
    ValueTask<List<Route>> GetEnabledRoutesOrderedByPriorityAsync();
}