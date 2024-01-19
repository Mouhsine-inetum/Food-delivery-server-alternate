using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FoodDeliveryServer.Core.CustomMiddleware
{
    internal class TokenCheckMiddleware
    {
        private readonly RequestDelegate _next;

        public TokenCheckMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            // Check if the user is authenticated
            if (context.User.Identity.IsAuthenticated)
            {
                var expirationClaim = context.User.FindFirst("exp");
                if (expirationClaim != null && long.TryParse(expirationClaim.Value, out long expirationTime))
                {
                    // Check if the token has expired
                    if (DateTime.UtcNow > DateTimeOffset.FromUnixTimeSeconds(expirationTime).UtcDateTime)
                    {
                        // Token has expired, perform logout
                        //await context.SignOutAsync(); // Assuming you are using cookie authentication
                        return;
                    }
                }
            }

            // Continue with the pipeline
            await _next(context);
        }
    }

    public static class TokenExpirationMiddlewareExtensions
    {
        public static IApplicationBuilder UseTokenExpirationMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TokenCheckMiddleware>();
        }
    }
}
