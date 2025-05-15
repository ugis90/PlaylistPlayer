using FleetManager.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FleetManager.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<FleetDbContext>));
            services.RemoveAll(typeof(FleetDbContext));

            string dbName = $"InMemoryDbForTesting_{Guid.NewGuid()}";
            services.AddDbContext<FleetDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
            });

            services.AddAuthorizationCore();
        });
        builder.UseEnvironment("Testing");
    }
}
