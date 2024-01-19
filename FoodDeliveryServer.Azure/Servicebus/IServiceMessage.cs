using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FoodDeliveryServer.Azure.Servicebus
{
    public interface IServiceMessage
    {
        Task SendMessageServiceBus(string[] message);
    }
}
