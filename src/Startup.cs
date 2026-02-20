using xmlTVGuide.Services;
using xmlTVGuide.Services.ArgumentParser;
using xmlTVGuide.Services.FileServices;
using xmlTVGuide.Services.ChannelMap;
using xmlTVGuide.Services.CronLogger;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;
using System.Xml.Linq;

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
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });

        // Add existing services
        services.AddSingleton<IAppArguments, ArgumentParser>();
        services.AddSingleton<IXmlTVBuilder, XmlTVBuilder>();
        services.AddSingleton<IFileService, XMLFileService>();
        services.AddSingleton<IChannelMapLoader, ChannelMapLoader>();
        services.AddSingleton<IDataFetcher, DataFetcher>();
        services.AddSingleton<ICronLogger, CronLogger>();
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