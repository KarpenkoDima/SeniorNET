using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SeniorNET.Application.Commands.CreateOrder;
using SeniorNET.Application.DTOs;
using SeniorNET.Application.Interfaces;
using SeniorNET.Infrastructure.Persistence;

namespace SeniorNET.Integration.Tests;

public class OrdersApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrdersApiTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static CreateOrderCommand CreateValidCommand() => new(
        CustomerId: "customer-integration-test",
        Street: "123 Test St",
        City: "TestCity",
        ZipCode: "00000",
        Country: "TestCountry",
        Items: [new CreateOrderItemCommand(Guid.NewGuid(), "Test Widget", 19.99m, "USD", 3)]);

    [Fact]
    public async Task CreateOrder_ValidRequest_ShouldReturn201()
    {
        var command = CreateValidCommand();

        var response = await _client.PostAsJsonAsync("/api/orders", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        order.Should().NotBeNull();
        order!.CustomerId.Should().Be("customer-integration-test");
        order.Items.Should().HaveCount(1);
        order.TotalAmount.Should().Be(59.97m);
    }

    [Fact]
    public async Task CreateOrder_EmptyCustomerId_ShouldReturn400()
    {
        var command = CreateValidCommand() with { CustomerId = "" };

        var response = await _client.PostAsJsonAsync("/api/orders", command);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOrder_ExistingOrder_ShouldReturn200()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/orders", CreateValidCommand());
        var created = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        var response = await _client.GetAsync($"/api/orders/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        order!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetOrder_NonExistent_ShouldReturn404()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelOrder_ExistingOrder_ShouldReturn204()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/orders", CreateValidCommand());
        var created = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        var response = await _client.PostAsync($"/api/orders/{created!.Id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetOrders_ShouldReturnPagedResult()
    {
        await _client.PostAsJsonAsync("/api/orders", CreateValidCommand());
        await _client.PostAsJsonAsync("/api/orders", CreateValidCommand());

        var response = await _client.GetAsync("/api/orders?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthCheck_ShouldReturn200()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real DbContext
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Remove MassTransit
            var massTransitDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("MassTransit") == true)
                .ToList();
            foreach (var d in massTransitDescriptors) services.Remove(d);

            // Add InMemory database
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));

            // Mock cache to use in-memory
            var cacheService = Substitute.For<ICacheService>();
            cacheService.GetAsync<OrderDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((OrderDto?)null);
            services.AddSingleton(cacheService);
        });
    }
}
