using FoodDeliveryServer.Azure.Servicebus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FoodDeliveryServer.Azure
{
    public static class AzureDependencyInjection
    {
        //public static IServiceProvider (
        public static IServiceCollection AddAzureService(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IServiceMessage,ServiceMessage>();

            return services;
        }
    }
}
