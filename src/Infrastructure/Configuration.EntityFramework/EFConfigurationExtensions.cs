using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.Configuration;

public static class EFConfigurationExtensions
{
    public static IConfigurationBuilder AddEFConfiguration<TDbContext, TEntity, TConfig>(
        this IConfigurationBuilder builder,
        Func<TDbContext> dbContextFactory,
        Expression<Func<TDbContext, IQueryable<TEntity>>> queryExpression,
        Func<List<TEntity>, TConfig> mapper,
        string? sectionName = null,
        TimeSpan? refreshInterval = null)
        where TDbContext : DbContext
        where TEntity : class
        where TConfig : class
    {
        var options = new EFConfigurationOptions<TDbContext, TEntity, TConfig>
        {
            DbContextFactory = dbContextFactory,
            Query = queryExpression,
            Mapper = mapper,
            SectionName = sectionName ?? typeof(TConfig).Name,
            RefreshInterval = refreshInterval ?? TimeSpan.FromSeconds(5)
        };

        return builder.Add(new EFConfigurationSource<TDbContext, TEntity, TConfig>(options));
    }

    public static IConfigurationBuilder AddEFConfiguration<TDbContext, TEntity, TConfig>(
        this IConfigurationBuilder builder,
        Action<EFConfigurationOptions<TDbContext, TEntity, TConfig>> configure)
        where TDbContext : DbContext
        where TEntity : class
        where TConfig : class
    {
        var options = new EFConfigurationOptions<TDbContext, TEntity, TConfig>();
        configure(options);

        if (options.DbContextFactory == null)
            throw new InvalidOperationException("DbContextFactory is required");

        if (options.Query == null)
            throw new InvalidOperationException("Query expression is required");

        if (options.Mapper == null && !options.UseAutoMapper)
            throw new InvalidOperationException("Mapper function is required when not using AutoMapper");

        return builder.Add(new EFConfigurationSource<TDbContext, TEntity, TConfig>(options));
    }
}