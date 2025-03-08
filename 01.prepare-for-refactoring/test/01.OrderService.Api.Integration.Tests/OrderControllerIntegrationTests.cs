using System.Net.Http.Json;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using OrderService.Api.Integration.Tests.Fixtures;
using OrderService.Api.Models.Entities;
using Respawn;
using Respawn.Graph;

namespace OrderService.Api.Integration.Tests;

public class OrderControllerIntegrationTests : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    private readonly IntegrationTestFixture fixture;
    private Respawner? respawner;
    private readonly Fixture autoFixture = new();

    public OrderControllerIntegrationTests(IntegrationTestFixture fixture)
    {
        this.fixture = fixture;
    }
    public async Task DisposeAsync()
    {
        await respawner!.ResetAsync(fixture.SqlServerFixture.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        if (!fixture.Initialized)
            await fixture.StartAsync();
        respawner = await Respawner.CreateAsync(fixture.SqlServerFixture.GetConnectionString(), new RespawnerOptions(){
            TablesToInclude = ["Orders"],
            SchemasToInclude = ["Order"]
        });
    }

    [Fact]
    public async Task GetOrders_WhenOrdersListIsEmpty_ThenReturnsOkWithEmptyResult()
    {
        // Arrange
        var response = await fixture.Client!.GetAsync("/api/order");
        response.EnsureSuccessStatusCode();

        // Act
        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();

        // Assert
        orders.Should().NotBeNull();
        orders.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrders_WhenOrdersListIsNotEmpty_ThenReturnsOkWithResult()
    {
        // Arrange

        var expectedOrders = autoFixture.Build<Order>()
            .With(x => x.OrderNumber, autoFixture.Create<string>().PadRight(20).Substring(0, 20))
            .With(x => x.CustomerPhone, autoFixture.Create<string>().PadRight(20).Substring(0, 20))
            .Without(x => x.OrderItems)
            .CreateMany().ToList();
        await MockOrders(expectedOrders);

        // Act
        var response = await fixture.Client!.GetAsync("/api/order");
        response.EnsureSuccessStatusCode();

        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();

        // Assert
        orders.Should().NotBeNull();
        orders.Count.Should().Be(expectedOrders.Count);
        orders.Should().BeEquivalentTo(expectedOrders, options =>
        {
            options.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromSeconds(1))).WhenTypeIs<DateTime>();
            options.Excluding(o => o.Id);
            return options;
        });
        

    }

    private async Task MockOrders(List<Order> expectedOrders)
    {
        foreach (var order in expectedOrders)
        {
            using var sqlConnection = new SqlConnection(fixture.SqlServerFixture.GetConnectionString());
            await sqlConnection.OpenAsync();
            //using var command = new SqlCommand($"INSERT INTO [Order].Orders (OrderNumber, OrderDate, CustomerName, CustomerAddress, CustomerEmail, CustomerPhone) VALUES ('{order.OrderNumber}', '{order.OrderDate}', '{order.CustomerName}', '{order.CustomerAddress}', '{order.CustomerEmail}', '{order.CustomerPhone}')", sqlConnection);
            using var command = new SqlCommand($"INSERT INTO [Order].Orders (OrderNumber, OrderDate, CustomerName, CustomerAddress, CustomerEmail, CustomerPhone, Status) VALUES (@OrderNumber, @OrderDate, @CustomerName, @CustomerAddress, @CustomerEmail, @CustomerPhone, @Status)", sqlConnection);
            command.Parameters.AddWithValue("@OrderNumber", order.OrderNumber);
            command.Parameters.AddWithValue("@OrderDate", order.OrderDate);
            command.Parameters.AddWithValue("@CustomerName", order.CustomerName);
            command.Parameters.AddWithValue("@CustomerAddress", order.CustomerAddress);
            command.Parameters.AddWithValue("@CustomerEmail", order.CustomerEmail);
            command.Parameters.AddWithValue("@CustomerPhone", order.CustomerPhone);
            command.Parameters.AddWithValue("@Status", order.Status);

            await command.ExecuteNonQueryAsync();
        }
    }
}
