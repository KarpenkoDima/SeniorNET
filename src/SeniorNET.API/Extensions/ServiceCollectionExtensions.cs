namespace SeniorNET.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddHealthChecks();
        services.AddEndpointsApiExplorer();

        return services;
    }
}
