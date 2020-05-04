using Microsoft.Extensions.Configuration;

namespace Spike.BulkVsSingle.MessageHandling.Configuration
{
    public class Configuration
    {
        private IConfigurationRoot config;

        public IConfigurationRoot Config => config ?? (config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build());

        public string EndpointName => Config.GetSection("AppSettings")["EndpointName"];
        public string ServiceBusConnectionString => Config.GetConnectionString("ServiceBusConnectionString");
        public string StorageConnectionString => Config.GetConnectionString("StorageConnectionString");
        public string SqlConnectionString => Config.GetConnectionString("PaymentsConnectionString");
    }
}