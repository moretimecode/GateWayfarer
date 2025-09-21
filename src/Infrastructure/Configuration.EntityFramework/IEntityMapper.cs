using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace Microsoft.Extensions.Configuration;

public interface IEntityMapper<TEntity, TConfig> 
    where TEntity : class 
    where TConfig : class
{
    TConfig Map(List<TEntity> entities);
}