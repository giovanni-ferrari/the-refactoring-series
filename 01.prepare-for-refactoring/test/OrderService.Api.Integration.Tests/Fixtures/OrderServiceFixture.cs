using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OrderService.Api.Integration.Tests.Fixtures;

public class OrderServiceFixture : WebApplicationFactory<Program>
{
    private readonly string connectionString;

    public OrderServiceFixture(string connectionString)
    {
        this.connectionString = connectionString;
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            });
        });
        base.ConfigureWebHost(builder);
    }
}
