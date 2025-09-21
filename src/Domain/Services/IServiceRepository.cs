using Droplet.Data.Repositories;

namespace Domain.Services
{
    public interface IServiceRepository: IRepository<Service, int>
    {
        Task<List<Service>> GetEnabledServicesAsync();
        ValueTask<Service?> GetByCodeAsync(string code);
        Task<bool> ExistsAsync(string code);
    }
}