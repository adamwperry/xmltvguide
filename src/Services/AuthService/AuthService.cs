namespace XmlTvGuide.Generator.Services.AuthService;

using XmlTvGuide.Generator.Models;

public class AuthService : IAuthService
{
    private readonly string _adminUsername;
    private readonly string _adminPassword;
    private readonly string _adminEmail;

    public AuthService(IConfiguration configuration)
    {
        // Priority: env vars (Docker/prod) > appsettings.json (local dev)
        _adminUsername = Environment.GetEnvironmentVariable("AUTH_USERNAME") ?? configuration["Auth:Username"] ?? throw new InvalidOperationException("Auth:Username not configured");
        _adminPassword = Environment.GetEnvironmentVariable("AUTH_PASSWORD") ?? configuration["Auth:Password"] ?? throw new InvalidOperationException("Auth:Password not configured");
        _adminEmail = Environment.GetEnvironmentVariable("AUTH_EMAIL") ?? configuration["Auth:Email"] ?? throw new InvalidOperationException("Auth:Email not configured");
    }

    public bool ValidateCredentials(string username, string password)
    {
        return username == _adminUsername && password == _adminPassword;
    }

    public User GetUserByUsername(string username)
    {
        if (username == _adminUsername)
            return new User
            {
                Username = _adminUsername,
                Email = _adminEmail
            };

        throw new InvalidOperationException($"User '{username}' not found.");
    }
}
