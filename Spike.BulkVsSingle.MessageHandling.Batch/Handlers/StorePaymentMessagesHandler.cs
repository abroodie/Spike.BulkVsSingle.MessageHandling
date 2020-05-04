using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Spike.BulkVsSingle.MessageHandling.Data;
using Spike.BulkVsSingle.MessageHandling.Messages;

namespace Spike.BulkVsSingle.MessageHandling.Batch.Handlers
{
    public class StorePaymentMessagesHandler
    {
        private static Configuration.Configuration config = new Configuration.Configuration();
        public async Task Handle(IList<PaymentMessage> messages, CancellationToken cancellationToken)
        {
            Console.WriteLine($"{DateTime.Now:s}, Received {messages.Count} payments");
            var stopwatch = Stopwatch.StartNew();

            await using (var dataContext = new DataContext(config.SqlConnectionString))
            {
                await dataContext.Payment.AddRangeAsync(messages,cancellationToken);
                await dataContext.SaveChangesAsync(cancellationToken);
            }
            Console.WriteLine($"{DateTime.Now:s}, Saved {messages.Count} payments, took: {stopwatch.ElapsedMilliseconds}ms");
        }
    }
}