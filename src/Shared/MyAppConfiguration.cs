using Droplet.AppConfiguration;
using Microsoft.Extensions.Configuration;

namespace Shared
{
    public class MyAppConfiguration : BaseAppConfiguration<MyAppConfiguration>
    {
        public override void Bind(IConfiguration config)
        {

        }
    }
}
