using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NServiceBus;
using Spike.BulkVsSingle.MessageHandling.Data;
using Spike.BulkVsSingle.MessageHandling.Messages;

namespace Spike.BulkVsSingle.MessageHandling.NSB.Handlers
{
    public class StorePaymentMessageHandler: IHandleMessages<PaymentMessage>
    {
        private static Configuration.Configuration config = new Configuration.Configuration();
        public async Task Handle(PaymentMessage message, IMessageHandlerContext context)
        {
            Console.WriteLine($"{DateTime.Now:s}, Received message: {JsonConvert.SerializeObject(message)}");
            var stopwatch = Stopwatch.StartNew();
            await using (var dataContext = new DataContext(config.SqlConnectionString))
            {
                await dataContext.Payment.AddAsync(message);
                await dataContext.SaveChangesAsync();
            }
            Console.WriteLine($"{DateTime.Now:s}, Saved message: {message.Id}, took: {stopwatch.ElapsedMilliseconds}ms");
        }
    }
}