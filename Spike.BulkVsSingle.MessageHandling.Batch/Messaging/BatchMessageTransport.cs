﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.InteropExtensions;
using Microsoft.Azure.ServiceBus.Management;
using Spike.BulkVsSingle.MessageHandling.Batch.Handlers;
using Spike.BulkVsSingle.MessageHandling.Messages;

namespace Spike.BulkVsSingle.MessageHandling.Batch.Messaging
{
    public class BatchMessageTransport
    {
        private readonly Configuration.Configuration config;
        private CancellationToken startingCancellationToken;
        private readonly string connectionString;
        private MessageDeserializer messageDeserializer;

        private readonly string endpointName;

        public BatchMessageTransport(Configuration.Configuration config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            messageDeserializer = new MessageDeserializer();
            endpointName = config.EndpointName;
            connectionString = config.ServiceBusConnectionString;
        }

        public async Task Run(CancellationToken cancellationToken)
        {
            this.startingCancellationToken = cancellationToken;
            await EnsureQueue(endpointName).ConfigureAwait(false);
            await Listen(cancellationToken).ConfigureAwait(false);
        }

        private async Task<List<(Object Message, BatchMessageReceiver Receiver, Message ReceivedMessage)>> ReceiveMessages(BatchMessageReceiver messageReceiver, CancellationToken cancellationToken)
        {
            var applicationMessages = new List<(Object Message, BatchMessageReceiver Receiver, Message ReceivedMessage)>();
            var messages = await messageReceiver.ReceiveMessages(200, cancellationToken).ConfigureAwait(false);
            if (!messages.Any())
                return applicationMessages;

            foreach (var message in messages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var applicationMessage = GetApplicationMessage(message);
                    applicationMessages.Add((applicationMessage, messageReceiver, message));
                }
                catch (Exception e)
                {
                    //logger.LogError($"Error deserialising the message. Error: {e.Message}", e);
                    //TODO: should use the error queue instead of dead letter queue
                    await messageReceiver.DeadLetter(message)
                        .ConfigureAwait(false);
                }
            }

            return applicationMessages;
        }

        private async Task Listen(CancellationToken cancellationToken)
        {
            var connection = new ServiceBusConnection(connectionString);
            var messageReceivers = new List<BatchMessageReceiver>();
            messageReceivers.AddRange(Enumerable.Range(0, 3)
                .Select(i => new BatchMessageReceiver(connection, endpointName)));
            var errorQueueSender = new MessageSender(connection, endpointName+"-errors", RetryPolicy.Default);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var pipeLineStopwatch = Stopwatch.StartNew();
                        var receiveTimer = Stopwatch.StartNew();

                        var receiveTasks =
                            messageReceivers.Select(receiver => ReceiveMessages(receiver, cancellationToken)).ToList();
                        await Task.WhenAll(receiveTasks).ConfigureAwait(false);

                        var messages = receiveTasks.SelectMany(task => task.Result).ToList();
                        receiveTimer.Stop();
                        if (!messages.Any())
                        {
                            await Task.Delay(500, cancellationToken);
                            continue;
                        }
                        RecordMetric("ReceiveMessages", receiveTimer.ElapsedMilliseconds, messages.Count);
                        var groupedMessages = new Dictionary<Type, List<(object Message, BatchMessageReceiver MessageReceiver, Message ReceivedMessage)>>();
                        foreach (var message in messages)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var key = message.Message.GetType();
                            var applicationMessages = groupedMessages.ContainsKey(key)
                                ? groupedMessages[key]
                                : groupedMessages[key] = new List<(object Message, BatchMessageReceiver MessageReceiver, Message ReceivedMessage)>();
                            applicationMessages.Add(message);
                        }

                        var stopwatch = Stopwatch.StartNew();
                        await Task.WhenAll(groupedMessages.Select(group =>
                            ProcessMessages(group.Key, group.Value, cancellationToken)));
                        stopwatch.Stop();
                        RecordMetric("ProcessedAllBatches", stopwatch.ElapsedMilliseconds, messages.Count);
                        pipeLineStopwatch.Stop();
                        RecordMetric("ProcessedPipeline",pipeLineStopwatch.ElapsedMilliseconds, messages.Count);
                    }
                    catch (TaskCanceledException)
                    {
                        //logger.LogWarning("Cancelling communication listener.");
                        Console.WriteLine("Cancelling communication listener.");
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        //logger.LogWarning("Cancelling communication listener.");
                        Console.WriteLine("Cancelling communication listener.");
                        return;
                    }
                    catch (Exception ex)
                    {
                        //logger.LogError($"Error listening for message.  Error: {ex.Message}", ex);
                        Console.WriteLine($"Error listening for message.  Error: {ex.Message}", ex);
                    }
                }
            }
            finally
            {
                await Task.WhenAll(messageReceivers.Select(receiver => receiver.Close())).ConfigureAwait(false);
                if (!connection.IsClosedOrClosing)
                    await connection.CloseAsync();
            }
        }
        private void RecordMetric(string eventName, long elapsedMilliseconds, int count, Action<Dictionary<string, string>, Dictionary<string, double>> metricsAction = null)
        {
            Console.WriteLine($"{DateTime.Now:s}. {eventName} - count: {count}, duration: {elapsedMilliseconds}ms");
            //var metrics = new Dictionary<string, double>
            //{
            //    {TelemetryKeys.Duration, elapsedMilliseconds},
            //    {TelemetryKeys.Count, count}
            //};
            //var properties = new Dictionary<string, string>();
            //metricsAction?.Invoke(properties, metrics);
            //telemetry.TrackEvent($"{TelemetryPrefix}.{eventName}", properties, metrics);
        }

        private object GetApplicationMessage(Message message)
        {
            return DeserializeMessage(message);
        }

        private object DeserializeMessage(Message message)
        {
            return messageDeserializer.DeserializeMessage(message);
        }

        protected async Task ProcessMessages(Type groupType, List<(object Message, BatchMessageReceiver MessageReceiver, Message ReceivedMessage)> messages,
    CancellationToken cancellationToken)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                //using (var containerScope = scopeFactory.CreateScope())
                {
                    //                    if (!containerScope.TryResolve(typeof(IHandleMessageBatches<>).MakeGenericType(groupType),
                    //    out object handler))
                    //{
                    //    //logger.LogError($"No handler found for message: {groupType.FullName}");
                    //    await Task.WhenAll(messages.Select(message => message.MessageReceiver.DeadLetter(message.ReceivedMessage)));
                    //    return;
                    //}

                    var handler = new StorePaymentMessagesHandler();
                    var methodInfo = handler.GetType().GetMethod("Handle");
                    if (methodInfo == null)
                        throw new InvalidOperationException($"Handle method not found on handler: {handler.GetType().Name} for message type: {groupType.FullName}");

                    var listType = typeof(List<>).MakeGenericType(groupType);
                    var list = (IList)Activator.CreateInstance(listType);
                    //var list = new List<PaymentMessage>();
                    messages.ForEach(message => list.Add(message.Message as PaymentMessage));

                    var handlerStopwatch = Stopwatch.StartNew();
                    await (Task)methodInfo.Invoke(handler, new object[] { list, cancellationToken });
//                    await handler.Handle(list, cancellationToken).ConfigureAwait(false);

                    RecordMetric(handler.GetType().FullName, handlerStopwatch.ElapsedMilliseconds, list.Count);
                    stopwatch.Restart();
                    await Task.WhenAll(messages.GroupBy(msg => msg.MessageReceiver).Select(group =>
                        group.Key.Complete(group.Select(msg => msg.ReceivedMessage.SystemProperties.LockToken)))).ConfigureAwait(false);
                    RecordMetric("CompletedMessages", stopwatch.ElapsedMilliseconds, messages.Count);
                }
            }
            catch (Exception e)
            {
                //logger.LogError($"Error processing messages. Error: {e.Message}", e);
                await Task.WhenAll(messages.GroupBy(msg => msg.MessageReceiver).Select(group =>
                        group.Key.Abandon(group.Select(msg => msg.ReceivedMessage.SystemProperties.LockToken)
                            .ToList())))
                    .ConfigureAwait(false);
            }
        }

        private string GetMessagePayload(Message receivedMessage)
        {
            const string transportEncodingHeaderKey = "NServiceBus.Transport.Encoding";
            var transportEncoding = receivedMessage.UserProperties.ContainsKey(transportEncodingHeaderKey)
                ? (string)receivedMessage.UserProperties[transportEncodingHeaderKey]
                : "application/octet-stream";
            byte[] messageBody;
            if (transportEncoding.Equals("wcf/byte-array", StringComparison.OrdinalIgnoreCase))
            {
                var doc = receivedMessage.GetBody<XmlElement>();
                messageBody = Convert.FromBase64String(doc.InnerText);
            }
            else
                messageBody = receivedMessage.Body;

            var monitoringMessageJson = Encoding.UTF8.GetString(messageBody);
            var sanitisedMessageJson = monitoringMessageJson
                .Trim(Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble())
                    .ToCharArray());
            return sanitisedMessageJson;
        }

        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            if (!startingCancellationToken.IsCancellationRequested)
                startingCancellationToken = cancellationToken;
        }

        private async Task EnsureQueue(string queuePath)
        {
            try
            {
                var manageClient = new ManagementClient(connectionString);
                if (await manageClient.QueueExistsAsync(queuePath, startingCancellationToken).ConfigureAwait(false))
                {
                    //logger.LogInfo($"Queue '{queuePath}' already exists, skipping queue creation.");
                    return;
                }

                //logger.LogInfo($"Creating queue '{queuePath}' with properties: TimeToLive: 7 days, Lock Duration: 5 Minutes, Max Delivery Count: 50, Max Size: 5Gb.");
                var queueDescription = new QueueDescription(queuePath)
                {
                    DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                    EnableDeadLetteringOnMessageExpiration = true,
                    LockDuration = TimeSpan.FromMinutes(5),
                    MaxDeliveryCount = 50,
                    MaxSizeInMB = 5120,
                    Path = queuePath
                };

                await manageClient.CreateQueueAsync(queueDescription, startingCancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                //logger.LogFatal($"Error ensuring queue: {e.Message}.", e);
                throw;
            }
        }
    }
}