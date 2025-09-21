using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Configuration;

public interface IConfigurationKeyGenerator
{
    Dictionary<string, string?> GenerateKeys<T>(T configObject) where T : class;
}