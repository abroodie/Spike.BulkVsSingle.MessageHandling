using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Features;

namespace Spike.BulkVsSingle.MessageHandling.NSB
{
    class Program
    {

        private static Configuration.Configuration config;
        static async Task Main(string[] args)
        {
            config = new Configuration.Configuration();
            try
            {
                Console.Title = "Spike.BulkVsSingle.MessageHandling.NSB";

                Console.WriteLine("Configuring Nservicebus");
                var endpointInstance = await ConfigureNsbForReceiver().ConfigureAwait(false);
                Console.WriteLine("Press Q to quit");
                while (true)
                {
                    if (Console.ReadKey().Key == ConsoleKey.Q)
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex);
                await Task.Delay(5000);
            }
            Console.WriteLine("Quitting.....");
            await Task.Delay(2000);

        }

        private static async Task<IEndpointInstance> ConfigureNsbForReceiver()
        {
            var endpointConfiguration = new EndpointConfiguration(config.EndpointName);

            var conventions = endpointConfiguration.Conventions();
            conventions.DefiningMessagesAs(type => type.Namespace?.StartsWith("Spike.BulkVsSingle.MessageHandling.Messages") ?? false);

            var persistence = endpointConfiguration.UsePersistence<AzureStoragePersistence>();
            persistence.ConnectionString(config.StorageConnectionString);
            var recoverability = endpointConfiguration.Recoverability();
            recoverability.Immediate(immediate => immediate.NumberOfRetries(0));
            recoverability.Delayed(delayed => delayed.NumberOfRetries(0));
            endpointConfiguration.Notifications.Errors.MessageSentToErrorQueue += Errors_MessageSentToErrorQueue;
            endpointConfiguration.DisableFeature<TimeoutManager>();
            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();
            transport
                .ConnectionString(config.ServiceBusConnectionString)
                .Transactions(TransportTransactionMode.ReceiveOnly)
                .PrefetchCount(20)
                .RuleNameShortener(ruleName => ruleName.Split('.').LastOrDefault() ?? ruleName);
            endpointConfiguration.SendFailedMessagesTo(config.EndpointName + "-errors");
            endpointConfiguration.UseSerialization<NewtonsoftSerializer>();
            endpointConfiguration.EnableInstallers();

            return await Endpoint.Start(endpointConfiguration).ConfigureAwait(false);
        }

        private static void Errors_MessageSentToErrorQueue(object sender, NServiceBus.Faults.FailedMessage e)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Message sent to error queue: {Encoding.UTF8.GetString(e.Body)}");
            Console.ResetColor();
        }
    }
}
