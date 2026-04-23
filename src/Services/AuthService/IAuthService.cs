namespace XmlTvGuide.Generator.Services.AuthService;

using XmlTvGuide.Generator.Models;

public interface IAuthService
{
    bool ValidateCredentials(string username, string password);
    User GetUserByUsername(string username);
}
