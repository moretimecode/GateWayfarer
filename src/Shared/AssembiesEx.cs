using System.Reflection;

namespace Shared
{
    public static class AssembiesEx
    {
        public static IEnumerable<Assembly> GetSolutionAssemblies()
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly =>
                    !assembly.IsDynamic &&
                    !string.IsNullOrEmpty(assembly.Location) &&
                    !IsSystemAssembly(assembly))
                .ToList();

            var targetAssemblies = new List<Assembly>();
            targetAssemblies.Add(Assembly.GetExecutingAssembly());
            var assemblyNames = new[]
            {
                "Api",
                "Application",
                "Infrastructure",
                "Domain",
                "Shared"
            };

            foreach (var name in assemblyNames)
            {
                try
                {
                    var assembly = Assembly.Load(name);
                    if (!targetAssemblies.Contains(assembly))
                    {
                        targetAssemblies.Add(assembly);
                    }
                }
                catch
                {
                }
            }

            return loadedAssemblies.Union(targetAssemblies).Distinct();
        }

        private static bool IsSystemAssembly(Assembly assembly)
        {
            var assemblyName = assembly.FullName?.ToLower() ?? "";
            var systemPrefixes = new[]
            {
                "system.",
                "microsoft.",
                "netstandard",
                "mscorlib",
                "presentationframework",
                "presentationcore",
                "windowsbase",
                "newtonsoft.json",
                "swashbuckle"
            };

            return systemPrefixes.Any(prefix => assemblyName.StartsWith(prefix));
        }
    }
}
