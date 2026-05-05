using xmlTVGuide.Services;
using xmlTVGuide.Services.ArgumentParser;
using xmlTVGuide.Services.AppSettings;
using xmlTVGuide.Services.FileServices;
using xmlTVGuide.Services.ChannelMap;
using xmlTVGuide.Services.CronLogger;
using xmlTVGuide.Services.BackgroundJobs;
using xmlTVGuide.Services.BuildJobLogger;
using xmlTVGuide.Services.Validation;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;
using XmlTvGuide.Generator.Services.AuthService;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace xmlTVGuide;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Add web services
        services.AddControllers();
        var allowedCorsOrigins = GetAllowedCorsOrigins();
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.WithOrigins(allowedCorsOrigins)
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .AllowCredentials();
            });
        });

        // Add authentication
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/login.html";
                options.LogoutPath = "/";
                options.AccessDeniedPath = "/login.html";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(24);
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;

                // Don't redirect on API calls, return 401 instead
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api"))
                            context.Response.StatusCode = 401;
                        else
                            context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api"))
                            context.Response.StatusCode = 403;
                        else
                            context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    }
                };
            });

        // Add existing services
        services.AddSingleton<IAppArguments, ArgumentParser>();
        services.AddSingleton<IAppSettingsService, FileAppSettingsService>();
        services.AddSingleton<IXmlTVBuilder, XmlTVBuilder>();
        services.AddSingleton<IFileService, XMLFileService>();
        services.AddSingleton<IChannelMapLoader, ChannelMapLoader>();
        services.AddSingleton<IDataFetcher, DataFetcher>();
        services.AddSingleton<ICronLogger, CronLogger>();
        services.AddSingleton<IBuildJobLogger, BuildJobLogger>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IEpgGenerationStatusTracker, InMemoryEpgGenerationStatusTracker>();
        services.AddSingleton<IEpgGenerationService, EpgGenerationService>();
        services.AddSingleton<IBackgroundJobService, BackgroundJobService>();
        services.AddSingleton<IValidationService, ValidationService>();
        services.AddTransient<IGuideParser, GuideOneParser>();
        services.AddTransient<IGuideParser, GuideTwoParser>();
        services.AddTransient<IGuideParser, GuideThreeParser>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();

        // Serve static files (our HTML interface)
        app.UseDefaultFiles();

        var staticFileOptions = new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                // Disable caching for development
                ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                ctx.Context.Response.Headers.Append("Pragma", "no-cache");
                ctx.Context.Response.Headers.Append("Expires", "0");
            }
        };
        app.UseStaticFiles(staticFileOptions);

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }

    private static string[] GetAllowedCorsOrigins()
    {
        var configuredOrigins = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
        if (!string.IsNullOrWhiteSpace(configuredOrigins))
        {
            var parsedOrigins = configuredOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();

            var invalidOrigins = parsedOrigins
                .Where(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                    string.IsNullOrWhiteSpace(uri.Scheme) ||
                    string.IsNullOrWhiteSpace(uri.Host))
                .ToArray();

            if (invalidOrigins.Length > 0)
            {
                throw new InvalidOperationException(
                    $"CORS_ALLOWED_ORIGINS contains invalid origin value(s): {string.Join(", ", invalidOrigins)}");
            }

            var validOrigins = parsedOrigins
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (validOrigins.Length == 0)
            {
                throw new InvalidOperationException(
                    "CORS_ALLOWED_ORIGINS was provided but did not contain any valid origins.");
            }

            return validOrigins;
        }

        return new[]
        {
            "http://localhost:8585",
            "http://localhost:8586",
            "http://127.0.0.1:8585",
            "http://127.0.0.1:8586"
        };
    }
}
