using AutoMapper;
using FluentValidation;
using FoodDeliveryServer.Data.Contexts;
using FoodDeliveryServer.Data.Interfaces;
using FoodDeliveryServer.Core.Mapping;
using FoodDeliveryServer.Data.Models;
using FoodDeliveryServer.Data.Repositories;
using FoodDeliveryServer.Core.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Stripe;
using System.Text;
using Customer = FoodDeliveryServer.Data.Models.Customer;
using CustomerService = FoodDeliveryServer.Core.Services.CustomerService;
using Product = FoodDeliveryServer.Data.Models.Product;
using ProductService = FoodDeliveryServer.Core.Services.ProductService;
using FoodDeliveryServer.Core.Interfaces;
using FoodDeliveryServer.Core.Services;
using FoodDeliveryServer.Common.Authorizations;
using Microsoft.AspNetCore.CookiePolicy;
using FoodDeliveryServer.Api;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Azure;
using Azure.Messaging.ServiceBus;
using Azure.Identity;
using FoodDeliveryServer.Azure;
using Azure.Core;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FoodDeliveryServer.Api", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        In = ParameterLocation.Header,
        Description = "Please enter token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme()
            {
                Reference = new OpenApiReference()
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});


///authentication from own idp

//builder.Services.AddAuthentication(options =>
//{
//    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
//    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
//}).AddJwtBearer(options =>
//{
//    options.TokenValidationParameters = new TokenValidationParameters()
//    {
//        ValidateIssuer = true,
//        ValidateAudience = true,
//        ValidateLifetime = true,
//        ValidateIssuerSigningKey = true,
//        ValidAudience = builder.Configuration["JWTSettings:ValidAudience"],
//        ValidIssuer = builder.Configuration["JWTSettings:ValidIssuer"],
//        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWTSettings:SecretKey"]))
//    };
//});


///authentication from auth0
///
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
//}).AddCookie(options =>
//{
//    options.Cookie.Name = "access_token";
//})
  .AddJwtBearer(options =>
{//using jwt bearer configuration
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters=new TokenValidationParameters()
    {
        ValidateAudience = true,
        ValidateIssuer = true,
    };
    options.ClaimsIssuer = builder.Configuration["Auth0:Authority"];
    options.Authority = builder.Configuration["Auth0:Authority"];
    options.Audience = builder.Configuration["Auth0:Audience"];

    //options.Events = new JwtBearerEvents()
    //{
    //    OnMessageReceived = context =>
    //    {
    //        if (context.Request.Cookies.ContainsKey("access_token"))
    //            context.Token = context.Request.Cookies["access_token"];
    //        return Task.CompletedTask;
    //    }
    //};
});




builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    //adding policiy with a middlwecare check instead of manually implementing it => any policiy can be implemented and managed through the middleware
    options.AddPolicy("AzureLogicApp", policy =>
                                            policy.Requirements.Add(new HasScopeRequirement(builder.Configuration["Auth0:Authority"], "LogicApp:read")));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "AllowClientApplication", corsBuilder =>
    {
        var clientDomain = builder.Configuration["ClientSettings:ClientDomain"];

        corsBuilder
            .WithOrigins(clientDomain)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IValidator<Admin>, AdminValidator>();

builder.Services.AddScoped<IPartnerService, PartnerService>();
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
builder.Services.AddScoped<IValidator<Partner>, PartnerValidator>();

builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IValidator<Customer>, CustomerValidator>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IValidator<User>, UserValidator>();

builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<IStoreRepository, StoreRepository>();
builder.Services.AddScoped<IValidator<Store>, StoreValidator>();

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IValidator<Product>, ProductValidator>();

builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IValidator<Order>, OrderValidator>();

builder.Services.AddScoped<IStripeService, StripeService>();

builder.Services.AddDbContext<FoodDeliveryDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("FoodDeliveryDbConnectionString"),x=>x.UseNetTopologySuite()));

MapperConfiguration mapperConfig = new MapperConfiguration(config =>
{
    config.AddProfile(new AdminProfile());
    config.AddProfile(new PartnerProfile());
    config.AddProfile(new CustomerProfile());
    config.AddProfile(new AuthProfile());
    config.AddProfile(new StoreProfile());
    config.AddProfile(new ProductProfile());
    config.AddProfile(new OrderProfile());
});

builder.Services.AddSingleton(mapperConfig.CreateMapper());
List<string> topics = builder.Configuration["ServiceBus:topics"].Split(' ').ToList();
builder.Services.AddAzureClients(clientBuilder =>
{
    // Register clients for each service
    clientBuilder.AddServiceBusClientWithNamespace(
        builder.Configuration["ServiceBus:Namespace"]);
    clientBuilder.UseCredential(new DefaultAzureCredential());

    // Register a subclient for each Service Bus Queue
    foreach (string topic in topics)
    {
        clientBuilder.AddClient<ServiceBusSender, ServiceBusClientOptions>(
            (_, _, provider) => provider.GetService<ServiceBusClient>()
                    .CreateSender(topic)).WithName(topic).ConfigureOptions(option =>
                    {
                        option.TransportType = ServiceBusTransportType.AmqpTcp;
                    });
    }
});

builder.Services.AddAzureService(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<FoodDeliveryDbContext>();
    context.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

StripeConfiguration.ApiKey = builder.Configuration["StripeSettings:SecretKey"];

app.UseHttpsRedirection();
app.UseCors("AllowClientApplication");

//app.UseCookiePolicy();

app.UseAuthentication();
//app.UseMiddleware<tokenmiddlware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
