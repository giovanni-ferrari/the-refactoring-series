using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using OrderService.Api.Integration.Tests.Fixtures;
using OrderService.Api.Models.Entities;
using Respawn;

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
        var expectedOrders = await MockOrders(10);

        // Act
        var response = await fixture.Client!.GetAsync("/api/order");
        response.EnsureSuccessStatusCode();

        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();

        // Assert
        orders.Should().NotBeNull();
        orders.Count.Should().Be(expectedOrders.Count());
        orders.Should().BeEquivalentTo(expectedOrders, options =>
        {
            options.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromSeconds(1))).WhenTypeIs<DateTime>();
            options.Excluding(o => o.Id);
            return options;
        });
    }

    [Fact]
    public async Task GetOrders_WhenOrdersItemsListIsNotEmpty_ReturnsOkResultOrdersWithoutOrderItems()
    {
        // Arrange
        var expectedOrders = await MockOrders(10);
        await MockOrderItems(expectedOrders);

        // Act
        var response = await fixture.Client!.GetAsync("/api/order");
        response.EnsureSuccessStatusCode();

        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();

        // Assert
        orders.Should().NotBeNull();
        orders.Count.Should().Be(expectedOrders.Count());
        orders.Select(o => o.OrderItems.Should().BeNull());
    }

    [Fact]  
    public async Task GetOrder_WhenOrderIdDoesNotExists_ThenReturnsNotFound()
    {
        // Arrange
        var orderId = 1;

        // Act
        var response = await fixture.Client!.GetAsync($"/api/order/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOrder_WhenOrderIdExists_ThenReturnsOkWithOrder()
    {
        // Arrange
        Order expectedOrder = (await MockOrders(1)).First();
        await MockOrderItems([expectedOrder]);

        // Act
        var response = await fixture.Client!.GetAsync($"/api/order/{expectedOrder.Id}");
        response.EnsureSuccessStatusCode();

        var order = await response.Content.ReadFromJsonAsync<Order>();

        // Assert
        order.Should().NotBeNull();
        order.Should().BeEquivalentTo(expectedOrder, options =>
        {
            options.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromSeconds(1))).WhenTypeIs<DateTime>();
            return options;
        });
    }

    [Fact]
    public async Task CreateOrder_WhenOrderIsNull_ThenReturnsBadRequest()
    {
        // Arrange
        Order? order = null;

        // Act
        var response = await fixture.Client!.PostAsJsonAsync("/api/order", order);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }   

    [Theory]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task CreateOrder_WhenStatusIsNotPending_ThenResturnsBadRequest(OrderStatus orderStatus)
    {
        // Arrange
        Order order = autoFixture.Build<Order>()
            .With(x => x.Status, orderStatus)
            .Create();

        // Act
        var response = await fixture.Client!.PostAsJsonAsync("/api/order", order);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_WhenOrderItemsAreNull_ThenReturnsBadRequest()
    {
        // Arrange
        Order order = autoFixture.Build<Order>()
            .Without(x => x.OrderItems)
            .Create();

        // Act
        var response = await fixture.Client!.PostAsJsonAsync("/api/order", order);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_WhenOrderItemsAreEmpty_ThenReturnsBadRequest()
    {
        // Arrange
        Order order = autoFixture.Build<Order>()
            .With(x => x.OrderItems, [])
            .Create();

        // Act
        var response = await fixture.Client!.PostAsJsonAsync("/api/order", order);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CraeteOrder_WhenOrderItemQuantityIsZero_ThenReturnsBadRequest()
    {
        // Arrange
        Order order = autoFixture.Build<Order>()
            .With(x => x.OrderItems, new List<OrderItem> { autoFixture.Build<OrderItem>().With(x => x.Quantity, 0).Create() })
            .Create();

        // Act
        var response = await fixture.Client!.PostAsJsonAsync("/api/order", order);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CraeteOrder_WhenOrderItemQuantityIsLessThanZero_ThenReturnsBadRequest()
    {
        // Arrange
        Order order = autoFixture.Build<Order>()
            .With(x => x.OrderItems, new List<OrderItem> { autoFixture.Build<OrderItem>().With(x => x.Quantity, -1).Create() })
            .Create();

        // Act
        var response = await fixture.Client!.PostAsJsonAsync("/api/order", order);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_WhenOrderIsValid_ThenReturnsOkCreated()
    {
        // Arrange
        Order order = autoFixture.Build<Order>()
            .With(x => x.Status, OrderStatus.Pending)
            .With(x => x.CustomerPhone, autoFixture.Create<string>().PadRight(20).Substring(0, 20))
            .With(x => x.OrderItems, new List<OrderItem> { autoFixture.Build<OrderItem>().With(x => x.Quantity, 1).Create() })
            .Create();

        // Act
        var response = await fixture.Client!.PostAsJsonAsync("/api/order", order);
        response.EnsureSuccessStatusCode();

        var createdOrder = await response.Content.ReadFromJsonAsync<Order>();

        // Assert
        createdOrder.Should().NotBeNull();
        createdOrder.Id.Should().BeGreaterThan(0);
        createdOrder.Should().BeEquivalentTo(order, options =>
        {
            options.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromSeconds(1))).WhenTypeIs<DateTime>();
            options.Excluding(o => o.Id);
            return options;
        });
    }

    private async Task<IEnumerable<Order>> MockOrders(int n)
    {
        var expectedOrders = autoFixture.Build<Order>()
                    .With(x => x.OrderNumber, autoFixture.Create<string>().PadRight(20).Substring(0, 20))
                    .With(x => x.CustomerPhone, autoFixture.Create<string>().PadRight(20).Substring(0, 20))
                    .Without(x => x.OrderItems)
                    .CreateMany(n)
                    .ToList();
        await PersistOrders(expectedOrders);
        return expectedOrders;
    }

    private async Task MockOrderItems(IEnumerable<Order> expectedOrders)
    {
        foreach(var expectedOrder in expectedOrders)
        {
            var expectedOrderItems = autoFixture.Build<OrderItem>()
                        .With(x => x.OrderId, expectedOrder.Id)
                        .With(x => x.ProductCode, autoFixture.Create<string>().PadRight(50).Substring(0, 50))
                        .With(x => x.ProductName, autoFixture.Create<string>().PadRight(100).Substring(0, 100))
                        .CreateMany().ToList();
            expectedOrder.OrderItems = expectedOrderItems;
            await PersistOrderItems(expectedOrderItems);
        }
        
    }

    private async Task PersistOrders(List<Order> expectedOrders)
    {
        foreach (var order in expectedOrders)
        {
            using var sqlConnection = new SqlConnection(fixture.SqlServerFixture.GetConnectionString());
            await sqlConnection.OpenAsync();
            using var command = new SqlCommand($"INSERT INTO [Order].Orders (OrderNumber, OrderDate, CustomerName, CustomerAddress, CustomerEmail, CustomerPhone, Status) OUTPUT INSERTED.Id VALUES (@OrderNumber, @OrderDate, @CustomerName, @CustomerAddress, @CustomerEmail, @CustomerPhone, @Status)", sqlConnection);
            command.Parameters.AddWithValue("@OrderNumber", order.OrderNumber);
            command.Parameters.AddWithValue("@OrderDate", order.OrderDate);
            command.Parameters.AddWithValue("@CustomerName", order.CustomerName);
            command.Parameters.AddWithValue("@CustomerAddress", order.CustomerAddress);
            command.Parameters.AddWithValue("@CustomerEmail", order.CustomerEmail);
            command.Parameters.AddWithValue("@CustomerPhone", order.CustomerPhone);
            command.Parameters.AddWithValue("@Status", order.Status);

            var insertedId = (int?)await command.ExecuteScalarAsync();
            if (insertedId is null) throw new Exception("Failed to insert order");
            order.Id = (int)insertedId;
        }
    }

    //mock order items
    private async Task PersistOrderItems(List<OrderItem> expectedOrderItems)
    {
        foreach (var orderItem in expectedOrderItems)
        {
            using var sqlConnection = new SqlConnection(fixture.SqlServerFixture.GetConnectionString());
            await sqlConnection.OpenAsync();
            using var command = new SqlCommand($"INSERT INTO [Order].OrderItems (OrderId, ProductCode, ProductName, Quantity) OUTPUT INSERTED.Id VALUES (@OrderId, @ProductCode, @ProductName, @Quantity)", sqlConnection);
            command.Parameters.AddWithValue("@OrderId", orderItem.OrderId);
            command.Parameters.AddWithValue("@ProductCode", orderItem.ProductCode);
            command.Parameters.AddWithValue("@Quantity", orderItem.Quantity);
            command.Parameters.AddWithValue("@ProductName", orderItem.ProductName);

            var insertedId = (int?)await command.ExecuteScalarAsync();
            if (insertedId is null) throw new Exception("Failed to insert order item");
            orderItem.Id = (int)insertedId;
        }
    }
}
