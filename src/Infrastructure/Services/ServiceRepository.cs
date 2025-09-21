using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Services;
using Domain.Services.Configuration;
using Droplet.Data.EntityFrameworkCore;
using Droplet.Data.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class ServiceRepository(IUnitOfWork unitOfWork, IOptionsMonitor<ServiceOptions> serviceOption) : EntityFrameworkCoreRepository<Service, int>(unitOfWork), IServiceRepository
    {
        private readonly ServiceOptions _services = serviceOption.CurrentValue;
        public async Task<bool> ExistsAsync(string code)
        {
            return await Table
               .AnyAsync(s => s.Code == code);
        }

        public ValueTask<Service?> GetByCodeAsync(string code)
        {
            var result = _services.Services.
                        Select(x =>
                            new Service
                            (
                                x.Code,
                                x.Name,
                                new HealthCheckConfig(
                                    x.HealthCheck.ActiveEnabled,
                                    x.HealthCheck.ActiveIntervalSeconds,
                                    x.HealthCheck.ActiveTimeoutSeconds,
                                    x.HealthCheck.ActivePath,
                                    x.HealthCheck.FailureThreshold,
                                    x.HealthCheck.SuccessThreshold,
                                    x.HealthCheck.PassiveEnabled,
                                    x.HealthCheck.PassiveFailureRateThreshold,
                                    x.HealthCheck.PassiveMinRequestCount,
                                    x.HealthCheck.PassiveReactivationPeriodSeconds,
                                    x.HealthCheck.Policy
                                ),
                                [.. x.Destinations.Select(a=>new Destination(
                                    a.Code,
                                    a.Protocol,
                                    a.Host,
                                    a.Port,
                                    a.Weight,
                                    a.TimeoutSeconds,
                                    a.HealthPath,
                                    a.Metadata))]))
                        .FirstOrDefault(s => s.Code == code);
            return ValueTask.FromResult(result);
        }

        public async Task<List<Service>> GetEnabledServicesAsync()
        {
            return await Table
               .Where(s => s.Destinations.Any())
               .OrderBy(s => s.Name)
               .ToListAsync();
        }
    }
}