using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.Configuration;

public class EFConfigurationSource<TDbContext, TEntity, TConfig> : IConfigurationSource
    where TDbContext : DbContext
    where TEntity : class
    where TConfig : class
{
    private readonly EFConfigurationOptions<TDbContext, TEntity, TConfig> _options;

    public EFConfigurationSource(EFConfigurationOptions<TDbContext, TEntity, TConfig> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new EFConfigurationProvider<TDbContext, TEntity, TConfig>(_options);
    }
}