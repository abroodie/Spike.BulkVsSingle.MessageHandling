using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.Configuration;
using NServiceBus;
using NServiceBus.Features;
using NUnit.Framework;
using Spike.BulkVsSingle.MessageHandling.Configuration;
using Spike.BulkVsSingle.MessageHandling.Data;
using Spike.BulkVsSingle.MessageHandling.Data.Entities;
using Spike.BulkVsSingle.MessageHandling.Messages;

namespace Spike.BulkVsSingle.MessageHandling.Tests
{
    [TestFixture]
    public class MessageHandlingTests
    {
        private static EndpointConfiguration endpointConfiguration;
        private static IEndpointInstance endpointInstance;
        private static Configuration.Configuration config;
        private DataContext dataContext;
        private static readonly int jobId = 9990999;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {

            config = new Configuration.Configuration();

            endpointConfiguration = new EndpointConfiguration(config.EndpointName);
            var conventions = endpointConfiguration.Conventions();
            conventions.DefiningMessagesAs(type => type.IsAssignableFrom(typeof(PaymentMessage)));


            endpointConfiguration.UsePersistence<AzureStoragePersistence>()
                .ConnectionString(config.StorageConnectionString);
            endpointConfiguration.DisableFeature<TimeoutManager>();


            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();
            transport
                .ConnectionString(config.ServiceBusConnectionString)
                .Transactions(TransportTransactionMode.ReceiveOnly)
                .RuleNameShortener(ruleName => ruleName.Split('.').LastOrDefault() ?? ruleName);

            var routing = transport.Routing();
            routing.RouteToEndpoint(typeof(PaymentMessage), config.GetNSBServiceEndpointName());
            endpointConfiguration.UseSerialization<NewtonsoftSerializer>();
            endpointConfiguration.EnableInstallers();
            endpointConfiguration.SendOnly();
            endpointInstance = await Endpoint.Start(endpointConfiguration);
        }

        private List<PaymentMessage> CreateMessages(int count)
        {
            return Enumerable.Range(0, count).Select(i => new PaymentMessage
            {
                AccountId = null,
                ActualEndDate = null,
                AgreementId = null,
                Amount = 1000,
                ApprenticeshipEmployerType = ApprenticeshipEmployerType.NonLevy,
                ApprenticeshipId = null,
                ApprenticeshipPriceEpisodeId = null,
                CollectionPeriod = new CollectionPeriod { AcademicYear = 1920, Period = 1 },
                CompletionAmount = 3000,
                CompletionStatus = null,
                ContractType = ContractType.Act2,
                DeliveryPeriod = 1,
                EarningEventId = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                EventTime = DateTimeOffset.UtcNow,
                FundingSource = FundingSourceType.CoInvestedSfa,
                FundingSourceEventId = Guid.NewGuid(),
                IlrSubmissionDateTime = DateTime.Now,
                InstalmentAmount = 1000,
                JobId = 999,
                LearnerReferenceNumber = "learn-ref-" + i.ToString(),
                LearnerUln = 9999990 + i,
                LearningAimFrameworkCode = 12,
                LearningAimFundingLineType = "Non-Levy",
                LearningAimPathwayCode = 52,
                LearningAimProgrammeType = 99,
                LearningAimReference = "learnref",
                LearningAimStandardCode = 12,
                LearningStartDate = DateTime.Today.AddYears(-1),
                NumberOfInstalments = 12,
                PlannedEndDate = DateTime.Today,
                PriceEpisodeIdentifier = "1/1/1/2020",
                ReportingAimFundingLineType = "-non-levy",
                SfaContributionPercentage = 1,
                StartDate = DateTime.Today.AddYears(-1),
                TransactionType = TransactionType.Learning,
                TransferSenderAccountId = null,
                Ukprn = 9999
            }).ToList();
        }

        [TestCase(100, 10, 0)]
        [TestCase(1000, 120, 1)]
        [TestCase(1000, 120, 2)]
        [TestCase(1000, 120, 3)]
        [TestCase(1000, 120, 4)]
        [TestCase(1000, 120, 5)]
        //[TestCase(100, 10, 1)]
        //[TestCase(100, 10, 2)]
        //[TestCase(100, 10, 3)]
        //[TestCase(100, 10, 4)]
        //[TestCase(100, 10, 5)]
        //[TestCase(100, 10, 6)]
        //[TestCase(100, 10, 7)]
        //[TestCase(100, 10, 8)]
        //[TestCase(100, 10, 9)]
        //[TestCase(100, 10, 10)]
        //[TestCase(500, 50, 11)]
        //[TestCase(500, 50, 12)]
        //[TestCase(500, 50, 13)]
        //[TestCase(500, 50, 14)]
        //[TestCase(500, 50, 15)]
        public async Task Time_For_NSB_To_Clear_Queue(int batchSize, int delayInSeconds, int testIndex)
        {
            Console.WriteLine($"Test: #{testIndex}, batch size: {batchSize}");
            var visibleTime = DateTime.UtcNow.AddSeconds(delayInSeconds);
            await SendMessages(batchSize, visibleTime).ConfigureAwait(false);
            var endTime = DateTime.Now.AddSeconds(delayInSeconds).Add(config.GetTimeToWait());
            Console.WriteLine($"Waiting until {endTime:G} for NSB service to finish storing the messages.");

            var connection = new ServiceBusConnection(config.ServiceBusConnectionString);
            var client = new ManagementClient(config.ServiceBusConnectionString);

            while (DateTime.Now < endTime)
            {
                var queueInfo = await client.GetQueueRuntimeInfoAsync(config.GetNSBServiceEndpointName())
                    .ConfigureAwait(false);
                Console.WriteLine($"Time: {DateTime.Now:G}. Queue count: {queueInfo.MessageCount}, Active messages: {queueInfo.MessageCountDetails.ActiveMessageCount}, Dead letter: {queueInfo.MessageCountDetails.DeadLetterMessageCount}, Scheduled: {queueInfo.MessageCountDetails.ScheduledMessageCount}");
                if (DateTime.UtcNow > visibleTime && queueInfo.MessageCount == 0)
                {
                    var executionTime = DateTime.UtcNow - visibleTime;
                    Console.WriteLine($"Time: {DateTime.Now:G}. Took: {executionTime.TotalSeconds} seconds to clear {batchSize} messages");
                    Assert.Pass();
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
            Assert.Fail("Failed to process all messages.");
        }

        [TestCase(100, 10, 0)]
        [TestCase(1000, 120, 1)]
        [TestCase(1000, 120, 2)]
        [TestCase(1000, 120, 3)]
        [TestCase(1000, 120, 4)]
        [TestCase(1000, 120, 5)]
        //[TestCase(100, 10, 1)]
        //[TestCase(100, 10, 2)]
        //[TestCase(100, 10, 3)]
        //[TestCase(100, 10, 4)]
        //[TestCase(100, 10, 5)]
        //[TestCase(100, 10, 6)]
        //[TestCase(100, 10, 7)]
        //[TestCase(100, 10, 8)]
        //[TestCase(100, 10, 9)]
        //[TestCase(100, 10, 10)]
        //[TestCase(500, 50, 11)]
        //[TestCase(500, 50, 12)]
        //[TestCase(500, 50, 13)]
        //[TestCase(500, 50, 14)]
        //[TestCase(500, 50, 15)]
        public async Task Time_For_Batch_Transport_To_Clear_Queue(int batchSize, int delayInSeconds, int testIndex)
        {
            Console.WriteLine($"Test: #{testIndex}, batch size: {batchSize}");
            var visibleTime = DateTime.UtcNow.AddSeconds(delayInSeconds);
            await SendMessages(batchSize, visibleTime, config.GetBatchServiceEndpointName()).ConfigureAwait(false);
            var endTime = DateTime.Now.AddSeconds(delayInSeconds).Add(config.GetTimeToWait());
            Console.WriteLine($"Waiting until {endTime:G} for NSB service to finish storing the messages.");

            var connection = new ServiceBusConnection(config.ServiceBusConnectionString);
            var client = new ManagementClient(config.ServiceBusConnectionString);

            while (DateTime.Now < endTime)
            {
                var queueInfo = await client.GetQueueRuntimeInfoAsync(config.GetBatchServiceEndpointName())
                    .ConfigureAwait(false);
                Console.WriteLine($"Time: {DateTime.Now:G}. Queue count: {queueInfo.MessageCount}, Active messages: {queueInfo.MessageCountDetails.ActiveMessageCount}, Dead letter: {queueInfo.MessageCountDetails.DeadLetterMessageCount}, Scheduled: {queueInfo.MessageCountDetails.ScheduledMessageCount}");
                if (DateTime.UtcNow > visibleTime && queueInfo.MessageCount == 0)
                {
                    var executionTime = DateTime.UtcNow - visibleTime;
                    Console.WriteLine($"Time: {DateTime.Now:G}. Took: {executionTime.TotalSeconds} seconds to clear {batchSize} messages");
                    Assert.Pass();
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
            Assert.Fail("Failed to process all messages.");
        }

        private async Task SendMessages(int batchSize, DateTimeOffset visibleTime, string endpointName = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var options = new NServiceBus.SendOptions();
            Console.WriteLine($"Messages visible at {visibleTime:G}");
            options.DoNotDeliverBefore(visibleTime);
            if (!string.IsNullOrEmpty(endpointName))
                options.SetDestination(endpointName);
            var messages = CreateMessages(batchSize);
            foreach (var message in messages)
            {
                await endpointInstance.Send(message, options).ConfigureAwait(false);
            }
            Console.WriteLine($"Sent {batchSize} messages at {DateTime.Now:G}.  Took: {stopwatch.ElapsedMilliseconds}ms");
        }
    }
}
