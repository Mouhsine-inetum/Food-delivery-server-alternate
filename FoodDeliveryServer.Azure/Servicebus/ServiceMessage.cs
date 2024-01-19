using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using FoodDeliveryServer.Azure.Models;

namespace FoodDeliveryServer.Azure.Servicebus
{
    public class ServiceMessage :IServiceMessage
    {
        private readonly ServiceBusClient client;

        // the sender used to publish messages to the queue
        private   readonly ServiceBusSender sender;
        //session id 
        private string session;

        public ServiceMessage(ServiceBusClient serviceBusClient)
        {
            client = serviceBusClient;
            //client = new ServiceBusClient("Endpoint=sb://foodelivery-europe-namespace.servicebus.windows.net/;SharedAccessKeyName=send-products;SharedAccessKey=alCdiC24D4q8IETkw/A/3BTHKIBsa4GlQ+ASbFsrf+M=;EntityPath=foodelivery-europe-topic");
            sender = client.CreateSender("foodelivery-europe-topic");
            session = "Foodelivery-";
        }


        public async Task SendMessageServiceBus(string[] message)
        {
            try
            {
                using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();
                await ProcessMessageBeforeSending(messageBatch,message);
                // Use the producer client to send the batch of messages to the Service Bus queue
                await sender.SendMessagesAsync(messageBatch);
            }

            catch(Exception ex)
            {
                var msg = ex.Message;
            }
            finally
            {
                // Calling DisposeAsync on client types is required to ensure that network
                // resources and other unmanaged objects are properly cleaned up.
                //await sender.DisposeAsync();
                //await client.DisposeAsync();
            }
        }


        private Task ProcessMessageBeforeSending(ServiceBusMessageBatch messageBatch,string[] message)
        {
            string idSession = Guid.NewGuid().ToString();
            session = session + message[0]+"-";

            for (int i = 1; i <= message.Length-1; i++)
            {
                var msg = new ServiceBusMessage($"{message[i]}");
                msg.MessageId = idSession+i.ToString();
                msg.SessionId = session+idSession;

                // try adding a message to the batch
                if (!messageBatch.TryAddMessage(msg))
                {
                    // if it is too large for the batch
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }


        //private Task MapDataToObjectFromMessages(string[] messages)
        //{
        //    ProductCreatedSBM product = new ProductCreatedSBM()
        //    {

        //    };
        //}
    }
}