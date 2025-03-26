namespace OrderService.Api.Integration.Tests.Fixtures;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    public OrderServiceFixture? OrderServiceFixture { get; private set; }
    public HttpClient? Client { get; private set; }
    public SqlServerFixture SqlServerFixture { get; }
    
    
    public IntegrationTestFixture()
    {
        SqlServerFixture = new SqlServerFixture();
    } 

    public async Task InitializeAsync()
    {
        await SqlServerFixture.InitializeAsync();
        OrderServiceFixture = new OrderServiceFixture(SqlServerFixture.GetConnectionString());  
        Client = OrderServiceFixture.CreateClient();
    }

    public async Task DisposeAsync()
    {
        await SqlServerFixture.DisposeAsync();
        await (OrderServiceFixture?.DisposeAsync() ?? ValueTask.CompletedTask);
        Client?.Dispose();
    }
}
