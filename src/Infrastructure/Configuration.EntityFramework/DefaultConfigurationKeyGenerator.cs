using System.Reflection;
using System.Collections;

namespace Microsoft.Extensions.Configuration;

public class DefaultConfigurationKeyGenerator(string sectionName) : IConfigurationKeyGenerator
{
    private readonly string _sectionName = sectionName ?? throw new ArgumentNullException(nameof(sectionName));

    public Dictionary<string, string?> GenerateKeys<T>(T configObject) where T : class
    {
        var result = new Dictionary<string, string?>();
        
        if (configObject == null)
            return result;

        GenerateKeysRecursive(configObject, _sectionName, result);
        return result;
    }

    private void GenerateKeysRecursive(object obj, string prefix, Dictionary<string, string?> result)
    {
        if (obj == null) return;

        var type = obj.GetType();

        if (IsSimpleType(type))
        {
            result[prefix] = obj.ToString();
            return;
        }
        
        if (obj is IEnumerable enumerable && !(obj is string))
        {
            var index = 0;
            foreach (var item in enumerable)
            {
                GenerateKeysRecursive(item, $"{prefix}:{index}", result);
                index++;
            }
            return;
        }

        // 处理复杂对象
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);

        foreach (var property in properties)
        {
            var value = property.GetValue(obj);
            var key = $"{prefix}:{property.Name}";
            
            if (value == null)
            {
                result[key] = null;
            }
            else if (IsSimpleType(property.PropertyType))
            {
                result[key] = value.ToString();
            }
            else
            {
                GenerateKeysRecursive(value, key, result);
            }
        }
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               Nullable.GetUnderlyingType(type) != null && IsSimpleType(Nullable.GetUnderlyingType(type)!);
    }
}