using FluentAssertions;
using Microsoft.Extensions.Configuration;
using XmlTvGuide.Generator.Services.AuthService;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly Dictionary<string, string?> _originalEnv = new()
    {
        ["AUTH_USERNAME"] = Environment.GetEnvironmentVariable("AUTH_USERNAME"),
        ["AUTH_PASSWORD"] = Environment.GetEnvironmentVariable("AUTH_PASSWORD"),
        ["AUTH_EMAIL"] = Environment.GetEnvironmentVariable("AUTH_EMAIL")
    };

    [Fact]
    public void uses_environment_variables_over_configuration_values()
    {
        Environment.SetEnvironmentVariable("AUTH_USERNAME", "env-admin");
        Environment.SetEnvironmentVariable("AUTH_PASSWORD", "env-secret");
        Environment.SetEnvironmentVariable("AUTH_EMAIL", "env@example.com");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Username"] = "config-admin",
                ["Auth:Password"] = "config-secret",
                ["Auth:Email"] = "config@example.com"
            })
            .Build();

        var service = new AuthService(configuration);

        service.ValidateCredentials("env-admin", "env-secret").Should().BeTrue();
        service.GetUserByUsername("env-admin").Email.Should().Be("env@example.com");
    }

    [Fact]
    public void validates_credentials_and_returns_user_from_configuration()
    {
        ClearEnv();
        var service = new AuthService(BuildConfiguration("admin", "changeme", "admin@example.com"));

        service.ValidateCredentials("admin", "changeme").Should().BeTrue();
        service.ValidateCredentials("admin", "wrong").Should().BeFalse();
        service.GetUserByUsername("admin").Should().BeEquivalentTo(new
        {
            Username = "admin",
            Email = "admin@example.com"
        });
    }

    [Fact]
    public void get_user_by_unknown_username_throws()
    {
        ClearEnv();
        var service = new AuthService(BuildConfiguration("admin", "changeme", "admin@example.com"));

        Action act = () => service.GetUserByUsername("other");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("User 'other' not found.");
    }

    [Fact]
    public void missing_configuration_throws()
    {
        ClearEnv();
        var configuration = new ConfigurationBuilder().Build();

        Action act = () => new AuthService(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Auth:Username not configured");
    }

    private static IConfiguration BuildConfiguration(string username, string password, string email)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Username"] = username,
                ["Auth:Password"] = password,
                ["Auth:Email"] = email
            })
            .Build();
    }

    private static void ClearEnv()
    {
        Environment.SetEnvironmentVariable("AUTH_USERNAME", null);
        Environment.SetEnvironmentVariable("AUTH_PASSWORD", null);
        Environment.SetEnvironmentVariable("AUTH_EMAIL", null);
    }

    public void Dispose()
    {
        foreach (var (key, value) in _originalEnv)
            Environment.SetEnvironmentVariable(key, value);
    }
}
