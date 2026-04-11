using ATMET.AI.Api.Tests.Fakes;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ATMET.AI.Api.Tests;

/// <summary>
/// Test host factory: replaces <see cref="IFoundryAgentReadService"/> so internal Foundry routes do not hit Supabase.
/// Merges <c>appsettings.Integration.json</c> from the test output directory so <c>ApiKeys:Keys</c> are configuration-driven.
/// </summary>
public sealed class AtmetApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var integrationPath = Path.Combine(
                AppContext.BaseDirectory,
                "appsettings.Integration.json");
            config.AddJsonFile(integrationPath, optional: false, reloadOnChange: false);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IFoundryAgentReadService>();
            services.AddSingleton<IFoundryAgentReadService, FakeFoundryAgentReadService>();
        });
    }
}
