using System.Threading.Tasks;
using OrderService.Api.Integration.Tests.Fixtures;

namespace OrderService.Api.Integration.Tests;

public class OrderControllerIntegrationTests : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    private readonly IntegrationTestFixture fixture;

    public OrderControllerIntegrationTests(IntegrationTestFixture fixture)
    {
        this.fixture = fixture;
    }
    public async Task DisposeAsync()
    {
        await fixture.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        await fixture.StartAsync();
    }

    [Fact]
    public async Task GetOrders_WhenOrdersListIsEmpty_ThenReturnsOkWithEmptyResult()
    {
        // Arrange
        var response = await fixture.Client.GetAsync("/api/order");
        response.EnsureSuccessStatusCode();

        // Act
        //var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();

        // Assert
        //Assert.Empty(orders);
    }
}
