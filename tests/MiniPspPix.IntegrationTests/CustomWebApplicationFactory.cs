using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniPspPix.Infrastructure.Messaging;
using MiniPspPix.Infrastructure.Persistence;

namespace MiniPspPix.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove o DbContext existente e substitui por InMemory isolado para cada teste
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PixDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            services.AddDbContext<PixDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            // Substitui o broker por InMemoryEventPublisher para testes de integração rápidos e sem dependência externa
            var publisherDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEventPublisher));
            if (publisherDescriptor != null)
            {
                services.Remove(publisherDescriptor);
            }
            services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });
    }
}

