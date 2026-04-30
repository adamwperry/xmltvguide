using xmlTVGuide.Services;
using xmlTVGuide.Services.ArgumentParser;
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
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.SetIsOriginAllowed(origin => origin.StartsWith("http://localhost"))
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
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseCors();

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
}
