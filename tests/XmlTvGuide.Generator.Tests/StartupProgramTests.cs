using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using xmlTVGuide;
using xmlTVGuide.Services;
using xmlTVGuide.Services.ArgumentParser;
using xmlTVGuide.Services.BackgroundJobs;
using xmlTVGuide.Services.BuildJobLogger;
using xmlTVGuide.Services.ChannelMap;
using xmlTVGuide.Services.CronLogger;
using xmlTVGuide.Services.FileServices;
using xmlTVGuide.Services.Validation;
using xmlTVGuide.Services.XMXTVBuilder;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;
using XmlTvGuide.Generator.Services.AuthService;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class StartupProgramTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Dictionary<string, string?> _originalEnv = new()
    {
        ["PORT"] = Environment.GetEnvironmentVariable("PORT"),
        ["DOTNET_RUNNING_IN_CONTAINER"] = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
        ["ASPNETCORE_ENVIRONMENT"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        ["CORS_ALLOWED_ORIGINS"] = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS"),
        ["AUTH_USERNAME"] = Environment.GetEnvironmentVariable("AUTH_USERNAME"),
        ["AUTH_PASSWORD"] = Environment.GetEnvironmentVariable("AUTH_PASSWORD"),
        ["AUTH_EMAIL"] = Environment.GetEnvironmentVariable("AUTH_EMAIL")
    };

    public StartupProgramTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"xmltvguide-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("AUTH_USERNAME", "admin");
        Environment.SetEnvironmentVariable("AUTH_PASSWORD", "changeme");
        Environment.SetEnvironmentVariable("AUTH_EMAIL", "admin@example.com");
    }

    [Fact]
    public void configure_services_registers_expected_dependencies()
    {
        var services = CreateServiceCollection();

        new Startup().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        provider.GetService<IAppArguments>().Should().BeOfType<ArgumentParser>();
        provider.GetService<IChannelMapLoader>().Should().BeOfType<ChannelMapLoader>();
        provider.GetService<ICronLogger>().Should().BeOfType<CronLogger>();
        provider.GetService<IBuildJobLogger>().Should().BeOfType<BuildJobLogger>();
        provider.GetService<IAuthService>().Should().BeOfType<AuthService>();
        provider.GetService<IEpgGenerationStatusTracker>().Should().NotBeNull();
        provider.GetService<IBackgroundJobService>().Should().BeOfType<BackgroundJobService>();
        provider.GetService<IValidationService>().Should().BeOfType<ValidationService>();
        provider.GetServices<IGuideParser>().Should().HaveCount(3);
    }

    [Fact]
    public async Task host_starts_successfully_in_production()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Production);
        Environment.SetEnvironmentVariable("PORT", GetUnusedLocalPort().ToString());
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "false");

        using var host = Program.CreateHostBuilder(Array.Empty<string>()).Build();
        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task host_starts_successfully_in_development()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);
        Environment.SetEnvironmentVariable("PORT", GetUnusedLocalPort().ToString());
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "false");

        using var host = Program.CreateHostBuilder(Array.Empty<string>()).Build();
        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public void create_host_builder_uses_src_wwwroot_locally()
    {
        Environment.SetEnvironmentVariable("PORT", "5010");
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "false");

        using var host = Program.CreateHostBuilder(Array.Empty<string>()).Build();
        var env = host.Services.GetRequiredService<IWebHostEnvironment>();

        env.WebRootPath.Should().Be(Path.Combine(Directory.GetCurrentDirectory(), "src", "wwwroot"));
    }

    [Fact]
    public void create_host_builder_uses_root_wwwroot_in_container_mode()
    {
        Environment.SetEnvironmentVariable("PORT", "5011");
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");

        using var host = Program.CreateHostBuilder(Array.Empty<string>()).Build();
        var env = host.Services.GetRequiredService<IWebHostEnvironment>();

        env.WebRootPath.Should().Be(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));
    }

    [Fact]
    public void configure_services_throws_clear_error_for_invalid_cors_origins()
    {
        Environment.SetEnvironmentVariable("CORS_ALLOWED_ORIGINS", "not-a-url,still-bad");
        Environment.SetEnvironmentVariable("PORT", "5012");
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "false");

        var act = () =>
        {
            using var host = Program.CreateHostBuilder(Array.Empty<string>()).Build();
        };

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*CORS_ALLOWED_ORIGINS contains invalid origin value(s)*");
    }

    [Fact]
    public async Task run_epg_generation_for_web_with_fake_data_generates_output()
    {
        var channelMapPath = Path.Combine(_tempDir, "ChannelMap.json");
        var outputPath = Path.Combine(_tempDir, "guide.xml");
        var fakeSourcePath = Path.Combine(Directory.GetCurrentDirectory(), "src", "TestData", "tvguide.json");
        await File.WriteAllTextAsync(channelMapPath, "{\"channels\":[]}");

        var result = await Program.RunEpgGenerationForWeb(new[]
        {
            "--fake",
            $"--url={fakeSourcePath}",
            $"--channelmap={channelMapPath}",
            $"--output={outputPath}"
        });

        result.Success.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();
        File.ReadAllText(outputPath).Should().Contain("<tv");
    }

    private static ServiceCollection CreateServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        return services;
    }

    private static int GetUnusedLocalPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        foreach (var (key, value) in _originalEnv)
            Environment.SetEnvironmentVariable(key, value);

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
