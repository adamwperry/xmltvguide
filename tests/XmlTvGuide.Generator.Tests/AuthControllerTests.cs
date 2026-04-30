using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using XmlTvGuide.Generator.Controllers;
using XmlTvGuide.Generator.Models;
using XmlTvGuide.Generator.Services.AuthService;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task login_with_missing_credentials_returns_bad_request()
    {
        var controller = CreateController(new Mock<IAuthService>(), new Mock<IAuthenticationService>());

        var result = await controller.Login(new LoginRequest { Username = "", Password = "" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task login_with_invalid_credentials_returns_unauthorized()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(service => service.ValidateCredentials("admin", "wrong")).Returns(false);

        var controller = CreateController(authService, new Mock<IAuthenticationService>());

        var result = await controller.Login(new LoginRequest { Username = "admin", Password = "wrong" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task login_with_valid_credentials_signs_in_and_returns_user()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(service => service.ValidateCredentials("admin", "changeme")).Returns(true);
        authService.Setup(service => service.GetUserByUsername("admin")).Returns(new User
        {
            Username = "admin",
            Email = "admin@example.com"
        });

        var authenticationService = new Mock<IAuthenticationService>();
        var controller = CreateController(authService, authenticationService);

        var result = await controller.Login(new LoginRequest
        {
            Username = "admin",
            Password = "changeme",
            RememberMe = true
        });

        result.Should().BeOfType<OkObjectResult>();
        authenticationService.Verify(service => service.SignInAsync(
            It.IsAny<HttpContext>(),
            CookieAuthenticationDefaults.AuthenticationScheme,
            It.Is<ClaimsPrincipal>(principal =>
                principal.Identity != null &&
                principal.Identity.Name == "admin" &&
                principal.FindFirst(ClaimTypes.Email) != null &&
                principal.FindFirst(ClaimTypes.Email)!.Value == "admin@example.com"),
            It.Is<AuthenticationProperties>(properties => properties.IsPersistent)), Times.Once);
    }

    [Fact]
    public async Task logout_signs_out_and_returns_ok()
    {
        var authenticationService = new Mock<IAuthenticationService>();
        var controller = CreateController(new Mock<IAuthService>(), authenticationService);
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "admin")
        }, "test"));

        var result = await controller.Logout();

        result.Should().BeOfType<OkObjectResult>();
        authenticationService.Verify(service => service.SignOutAsync(
            It.IsAny<HttpContext>(),
            CookieAuthenticationDefaults.AuthenticationScheme,
            null), Times.Once);
    }

    [Fact]
    public void get_current_user_returns_unauthorized_when_identity_missing()
    {
        var controller = CreateController(new Mock<IAuthService>(), new Mock<IAuthenticationService>());

        var result = controller.GetCurrentUser();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void get_current_user_returns_username_and_email()
    {
        var controller = CreateController(new Mock<IAuthService>(), new Mock<IAuthenticationService>());
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Email, "admin@example.com")
        }, "test"));

        var result = controller.GetCurrentUser().Should().BeOfType<OkObjectResult>().Subject;

        result.Value.Should().BeEquivalentTo(new
        {
            username = "admin",
            email = "admin@example.com"
        });
    }

    private static AuthController CreateController(Mock<IAuthService> authService, Mock<IAuthenticationService> authenticationService)
    {
        var services = new ServiceCollection()
            .AddSingleton(authenticationService.Object)
            .BuildServiceProvider();

        var controller = new AuthController(authService.Object, NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services
                }
            }
        };

        return controller;
    }
}
