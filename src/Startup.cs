using xmlTVGuide.Services;
using xmlTVGuide.Services.ArgumentParser;
using xmlTVGuide.Services.FileServices;
using xmlTVGuide.Services.ChannelMap;
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
        services.AddSingleton<IFileService, XMLFileService<XDocument>>();
        services.AddSingleton<IChannelMapLoader, ChannelMapLoader>();
        services.AddSingleton<IDataFetcher, DataFetcher>();
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
        app.UseStaticFiles();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}