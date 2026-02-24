using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeniorNET.Application.Interfaces;
using SeniorNET.Domain.Interfaces;
using SeniorNET.Infrastructure.Caching;
using SeniorNET.Infrastructure.Messaging;
using SeniorNET.Infrastructure.Persistence;
using SeniorNET.Infrastructure.Repositories;

namespace SeniorNET.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core + PostgreSQL
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        // Redis
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
            options.InstanceName = "SeniorNET:";
        });
        services.AddScoped<ICacheService, RedisCacheService>();

        // MassTransit + RabbitMQ
        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<OrderCreatedConsumer>();

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(configuration.GetConnectionString("RabbitMQ"));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
