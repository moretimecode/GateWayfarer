using Droplet.AppConfiguration;

namespace Shared
{
    public class MyAppConfigurationManager
    {
        public static MyAppConfiguration Config => _appConfigurationManager.Config;

        private static IAppConfigurationManager<MyAppConfiguration> _appConfigurationManager;
        public static void Initialize(IAppConfigurationManager<MyAppConfiguration> appConfigurationManager)
        {
            _appConfigurationManager = appConfigurationManager;
        }
    }
}
