namespace Microsoft.Extensions.Configuration;

public class DelegateEntityMapper<TEntity, TConfig>(Func<List<TEntity>, TConfig> mapperFunc) : IEntityMapper<TEntity, TConfig>
    where TEntity : class
    where TConfig : class
{
    private readonly Func<List<TEntity>, TConfig> _mapperFunc = mapperFunc ?? throw new ArgumentNullException(nameof(mapperFunc));

    public TConfig Map(List<TEntity> entities)
    {
        return _mapperFunc(entities);
    }
}