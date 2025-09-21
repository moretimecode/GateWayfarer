using Domain.Services.Services;
using Droplet.Data.Entities;

namespace Domain.Services
{
  public class Service : AggregateRoot
    {
        /// <summary>
        /// 服务的业务唯一标识
        /// </summary>
        public string Code { get; private set; }
        
        /// <summary>
        /// 服务名称
        /// </summary>
        public string Name { get; private set; }
        
        /// <summary>
        /// 健康检查配置
        /// </summary>
        public HealthCheckConfig HealthCheck { get; private set; }
        
        /// <summary>
        /// 目标实例列表
        /// </summary>
        private readonly List<Destination> _destinations = [];
        public IReadOnlyList<Destination> Destinations => _destinations.AsReadOnly();

        // EF Core 需要的私有构造函数
        private Service()
        {
            Code = string.Empty;
            Name = string.Empty;
            HealthCheck = new HealthCheckConfig();
        }

        public Service(
            string code,
            string name,
            HealthCheckConfig? healthCheck = null,
            List<Destination>? destinations = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Service code cannot be empty", nameof(code));
                
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Service name cannot be empty", nameof(name));

            Code = code;
            Name = name;
            HealthCheck = healthCheck ?? new HealthCheckConfig();
            
            if (destinations != null)
            {
                foreach (var destination in destinations)
                {
                    AddDestination(destination);
                }
            }
        }

        /// <summary>
        /// 添加目标实例
        /// </summary>
        public void AddDestination(Destination destination)
        {
            if (destination  is null)
                throw new ArgumentNullException(nameof(destination));
                
            // 检查Code是否重复
            if (_destinations.Any(d => d.Code == destination.Code))
                throw new InvalidOperationException($"Destination with code '{destination.Code}' already exists");
                
            _destinations.Add(destination);
        }

        /// <summary>
        /// 移除目标实例
        /// </summary>
        public bool RemoveDestination(string destinationCode)
        {
            var destination = _destinations.FirstOrDefault(d => d.Code == destinationCode);
            if (destination is not  null)
            {
                _destinations.Remove(destination);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 更新目标实例列表
        /// </summary>
        public void UpdateDestinations(List<Destination> newDestinations)
        {
            if (newDestinations == null)
                throw new ArgumentNullException(nameof(newDestinations));
                
            // 检查新列表中是否有重复的Code
            var duplicateCodes = newDestinations
                .GroupBy(d => d.Code)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
                
            if (duplicateCodes.Any())
                throw new InvalidOperationException($"Duplicate destination codes found: {string.Join(", ", duplicateCodes)}");

            _destinations.Clear();
            _destinations.AddRange(newDestinations);
        }

        /// <summary>
        /// 更新健康检查配置
        /// </summary>
        public void UpdateHealthCheckConfig(HealthCheckConfig healthCheckConfig)
        {
            HealthCheck = healthCheckConfig ?? throw new ArgumentNullException(nameof(healthCheckConfig));
        }

        /// <summary>
        /// 获取健康的目标实例列表（核心业务方法）
        /// </summary>
        public  List<Destination> GetHealthyDestinations(IHealthCalculationService healthService)
        {
            if (!_destinations.Any())
                return [];

            var healthyDestinations = new List<Destination>();

            foreach (var destination in _destinations)
            {
                var isHealthy =  healthService.IsHealthy(destination, HealthCheck);
                if (isHealthy)
                {
                    healthyDestinations.Add(destination);
                }
            }

            return healthyDestinations;
        }

        /// <summary>
        /// 根据Code获取目标实例
        /// </summary>
        public Destination? GetDestination(string destinationCode)
        {
            return _destinations.FirstOrDefault(d => d.Code == destinationCode);
        }

        /// <summary>
        /// 检查服务是否有可用的目标实例
        /// </summary>
        public bool HasDestinations()
        {
            return _destinations.Any();
        }
    }
}