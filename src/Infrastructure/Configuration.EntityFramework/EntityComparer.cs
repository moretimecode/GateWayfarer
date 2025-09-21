using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Configuration;

public class EntityComparer<TEntity> : IEqualityComparer<TEntity> where TEntity : class
{
    public bool Equals(TEntity? x, TEntity? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x == null || y == null) return false;

        var type = typeof(TEntity);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);

        foreach (var property in properties)
        {
            var xValue = property.GetValue(x);
            var yValue = property.GetValue(y);

            if (!Equals(xValue, yValue))
                return false;
        }

        return true;
    }

    public int GetHashCode(TEntity obj)
    {
        if (obj == null) return 0;

        var hash = new HashCode();
        var type = typeof(TEntity);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);

        foreach (var property in properties)
        {
            var value = property.GetValue(obj);
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}