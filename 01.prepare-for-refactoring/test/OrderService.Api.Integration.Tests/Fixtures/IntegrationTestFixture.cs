namespace OrderService.Api.Integration.Tests.Fixtures;

public sealed class IntegrationTestFixture : IFixture, IAsyncDisposable, IAsyncLifetime
{
    public OrderServiceFixture? OrderServiceFixture { get; private set; }
    public HttpClient? Client { get; private set; }
    public SqlServerFixture SqlServerFixture { get; }
    
    
    public IntegrationTestFixture()
    {
        SqlServerFixture = new SqlServerFixture();
    }
    public async ValueTask DisposeAsync()
    {
        await (OrderServiceFixture?.DisposeAsync() ?? ValueTask.CompletedTask);
        Client?.Dispose();
        await SqlServerFixture.DisposeAsync();
    }

    public async Task StartAsync()
    {
        await SqlServerFixture.StartAsync();
        OrderServiceFixture = new OrderServiceFixture(SqlServerFixture.GetConnectionString());  
        Client = OrderServiceFixture.CreateClient();
}

    public async Task InitializeAsync()
    {
        await StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await SqlServerFixture.DisposeAsync();
        await (OrderServiceFixture?.DisposeAsync() ?? ValueTask.CompletedTask);
        Client?.Dispose();
    }
}
