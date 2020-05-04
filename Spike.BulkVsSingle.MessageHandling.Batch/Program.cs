using System;
using System.Threading;
using System.Threading.Tasks;
using Spike.BulkVsSingle.MessageHandling.Batch.Messaging;

namespace Spike.BulkVsSingle.MessageHandling.Batch
{
    class Program
    {
        private static Configuration.Configuration config;

        static async Task Main(string[] args)
        {
            config = new Configuration.Configuration();
            try
            {
                config = new Configuration.Configuration();
                Console.Title = "Spike.BulkVsSingle.MessageHandling.Batch";

                Console.WriteLine("Configuring batch transport");
                var transport = new BatchMessageTransport(config);
                var cancellationTokenSource = new CancellationTokenSource();
                _ = transport.Run(cancellationTokenSource.Token);
                Console.WriteLine("Press Q to quit");
                while (true)
                {
                    if (Console.ReadKey().Key == ConsoleKey.Q)
                        break;
                }
                cancellationTokenSource.Cancel();
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
    }
}
