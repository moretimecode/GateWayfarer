using AutoMapper;
using System.Reflection;

namespace Microsoft.Extensions.Configuration;

public class AutoMapperEntityMapper<TEntity, TConfig> : IEntityMapper<TEntity, TConfig>
    where TEntity : class
    where TConfig : class
{
    private readonly IMapper _mapper;

    public AutoMapperEntityMapper(IMapper mapper)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public AutoMapperEntityMapper(params Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = new[] { Assembly.GetCallingAssembly(), Assembly.GetExecutingAssembly() };
        }

        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(assemblies);
        });

        config.AssertConfigurationIsValid();
        
        _mapper = config.CreateMapper();
    }

    public TConfig Map(List<TEntity> entities)
    {
        try
        {
            return _mapper.Map<TConfig>(entities);
        }
        catch (AutoMapperMappingException ex)
        {
            throw new InvalidOperationException(
                $"AutoMapper failed to map List<{typeof(TEntity).Name}> to {typeof(TConfig).Name}. " +
                $"Please ensure you have a proper Profile with mapping: CreateMap<List<{typeof(TEntity).Name}>, {typeof(TConfig).Name}>() " +
                $"Error: {ex.Message}", ex);
        }
    }
}