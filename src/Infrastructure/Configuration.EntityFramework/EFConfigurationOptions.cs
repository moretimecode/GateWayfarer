using System.Linq.Expressions;
using System.Reflection;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.Configuration;

public class EFConfigurationOptions<TDbContext, TEntity, TConfig>
    where TDbContext : DbContext
    where TEntity : class
    where TConfig : class
{
    public Expression<Func<TDbContext, IQueryable<TEntity>>>? Query { get; set; }  
    public Func<List<TEntity>, TConfig>? Mapper { get; set; }
    public Func<TDbContext>? DbContextFactory { get; set; }  
    public string SectionName { get; set; } = typeof(TConfig).Name;
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(5);
    public bool EnableChangeTracking { get; set; } = true;
    public Func<List<TEntity>, List<TEntity>, bool>? ChangeDetector { get; set; }
    public bool UseAutoMapper { get; set; } = false;
    public Assembly[]? AutoMapperAssemblies { get; set; }
    public IMapper? AutoMapperInstance { get; set; }
    
    public EFConfigurationOptions<TDbContext, TEntity, TConfig> WithQuery(
        Expression<Func<TDbContext, IQueryable<TEntity>>> query)  
    {
        Query = query;
        return this;
    }
    
    public EFConfigurationOptions<TDbContext, TEntity, TConfig> WithMapper(
        Func<List<TEntity>, TConfig> mapper)
    {
        Mapper = mapper;
        UseAutoMapper = false;
        return this;
    }
    
    public EFConfigurationOptions<TDbContext, TEntity, TConfig> WithDbContextFactory(
        Func<TDbContext> dbContextFactory)  
    {
        DbContextFactory = dbContextFactory;
        return this;
    }
    
    public EFConfigurationOptions<TDbContext, TEntity, TConfig> WithAutoMapper(params Assembly[] assemblies)
    {
        UseAutoMapper = true;
        AutoMapperAssemblies = assemblies.Length > 0 ? assemblies : new[] { Assembly.GetCallingAssembly() };
        Mapper = null;
        return this;
    }
    
    public EFConfigurationOptions<TDbContext, TEntity, TConfig> WithAutoMapper(IMapper mapper)
    {
        UseAutoMapper = true;
        AutoMapperInstance = mapper;
        Mapper = null;
        return this;
    }
    
    public EFConfigurationOptions<TDbContext, TEntity, TConfig> WithSection(string sectionName)
    {
        SectionName = sectionName;
        return this;
    }
    
    public EFConfigurationOptions<TDbContext, TEntity, TConfig> WithRefreshInterval(TimeSpan interval)
    {
        RefreshInterval = interval;
        return this;
    }

    public EFConfigurationOptions<TDbContext, TEntity, TConfig> WithChangeDetector(
        Func<List<TEntity>, List<TEntity>, bool> detector)
    {
        ChangeDetector = detector;
        return this;
    }
}