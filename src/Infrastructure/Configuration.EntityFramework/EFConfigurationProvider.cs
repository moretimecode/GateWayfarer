using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.Configuration;

public class EFConfigurationProvider<TDbContext, TEntity, TConfig> : ConfigurationProvider, IDisposable
    where TDbContext : DbContext
    where TEntity : class
    where TConfig : class
{
    private readonly EFConfigurationOptions<TDbContext, TEntity, TConfig> _options;
    private readonly Timer _refreshTimer;
    private DateTime _lastDataChange = DateTime.MinValue;
    private List<TEntity>? _lastData;
    private readonly IConfigurationKeyGenerator _keyGenerator;
    private readonly IEntityMapper<TEntity, TConfig> _mapper;

    public EFConfigurationProvider(EFConfigurationOptions<TDbContext, TEntity, TConfig> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _keyGenerator = new DefaultConfigurationKeyGenerator(_options.SectionName);

        _mapper = CreateMapper();

        _refreshTimer = new Timer(RefreshData, null, TimeSpan.Zero, _options.RefreshInterval);
    }

    public override void Load()
    {
        try
        {
            var entities = LoadEntitiesFromDatabase();
            var hasChanged = HasDataChanged(entities);

            if (hasChanged)
            {
                var configObject = _mapper.Map(entities);

                var configData = _keyGenerator.GenerateKeys(configObject);
                Data = configData;

                _lastData = entities.ToList();
                _lastDataChange = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load EF configuration: {ex.Message}");
        }
    }

    private List<TEntity> LoadEntitiesFromDatabase()
    {
        using var dbContext = _options.DbContextFactory!();

        var queryFunc = _options.Query!.Compile();
        var queryable = queryFunc(dbContext);

        return queryable.ToList();
    }

    private bool HasDataChanged(List<TEntity> newData)
    {
        if (_lastData == null) return true;

        if (_options.ChangeDetector != null)
            return _options.ChangeDetector(_lastData, newData);

        return _lastData.Count != newData.Count ||
               !_lastData.SequenceEqual(newData, new EntityComparer<TEntity>());
    }

    private void RefreshData(object? state)
    {
        if (DateTime.UtcNow - _lastDataChange < _options.RefreshInterval)
            return;

        var previousData = Data;
        Load();

        if (!DataEquals(previousData, Data))
        {
            OnReload();
        }
    }

    private IEntityMapper<TEntity, TConfig> CreateMapper()
    {
        if (_options.UseAutoMapper)
        {
            if (_options.AutoMapperInstance != null)
            {
                return new AutoMapperEntityMapper<TEntity, TConfig>(_options.AutoMapperInstance);
            }
            else if (_options.AutoMapperAssemblies != null)
            {
                return new AutoMapperEntityMapper<TEntity, TConfig>(_options.AutoMapperAssemblies);
            }
            else
            {
                return new AutoMapperEntityMapper<TEntity, TConfig>();
            }
        }
        else
        {
            return new DelegateEntityMapper<TEntity, TConfig>(_options.Mapper!);
        }
    }

    private bool DataEquals(IDictionary<string, string?>? data1, IDictionary<string, string?>? data2)
    {
        if (data1 == null && data2 == null) return true;
        if (data1 == null || data2 == null) return false;
        if (data1.Count != data2.Count) return false;

        return data1.All(kvp => data2.TryGetValue(kvp.Key, out var value) && value == kvp.Value);
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }
}