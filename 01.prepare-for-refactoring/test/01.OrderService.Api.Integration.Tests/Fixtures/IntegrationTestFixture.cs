using Microsoft.AspNetCore.Mvc.Testing;

namespace OrderService.Api.Integration.Tests.Fixtures;

public class IntegrationTestFixture : IFixture, IAsyncDisposable
{
    public OrderServiceFixture? Factory { get; private set; }
    public HttpClient? Client { get; private set; }
    public bool Initialized { get; private set; }
    public SqlServerFixture SqlServerFixture { get; }
    
    
    public IntegrationTestFixture()
    {
        SqlServerFixture = new SqlServerFixture();
    }
    public async ValueTask DisposeAsync()
    {
        await (Factory?.DisposeAsync() ?? ValueTask.CompletedTask);
        Client?.Dispose();
        await SqlServerFixture.DisposeAsync();
    }

    public async Task StartAsync()
    {
        await SqlServerFixture.StartAsync();
        Factory = new OrderServiceFixture(SqlServerFixture.GetConnectionString());  
        Client = Factory.CreateClient();
        Initialized = true;
    }
}
