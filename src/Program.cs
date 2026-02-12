using xmlTVGuide.Services;
using xmlTVGuide.Services.ArgumentParser;
using xmlTVGuide.Services.FileServices;
using System.Xml.Linq;
using xmlTVGuide.Services.ChannelMap;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;

namespace xmlTVGuide;

class Program
{
    static async Task Main(string[] args)
    {
        // Check if we should run as web host
        var runAsWeb = Environment.GetEnvironmentVariable("RUN_AS_WEB") == "true" || 
                       Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != null;

        if (runAsWeb)
        {
            // Start web host for configuration UI
            Console.WriteLine("Starting as web application...");
            await CreateHostBuilder(args).Build().RunAsync();
        }
        else
        {
            // Run as console application (for one-time execution)
            await RunEpgGeneration(args);
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
                webBuilder.UseUrls("http://0.0.0.0:8585");
                webBuilder.UseWebRoot(Path.Combine(Directory.GetCurrentDirectory(), "src", "wwwroot"));
            });

    public static async Task RunEpgGeneration(string[] args)
    {
        try
        {
            Console.WriteLine("Starting XMLTV Guide Generator...");
            Console.WriteLine($"EPG_URL_FILES: {Environment.GetEnvironmentVariable("EPG_URL_FILES")}");
            Console.WriteLine($"EPG_URL: {Environment.GetEnvironmentVariable("EPG_URL")}");
            Console.WriteLine($"CHANNEL_MAP_PATH: {Environment.GetEnvironmentVariable("CHANNEL_MAP_PATH")}");
            Console.WriteLine($"OUTPUT_PATH: {Environment.GetEnvironmentVariable("OUTPUT_PATH")}");

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IAppArguments, ArgumentParser>();
            serviceCollection.AddSingleton<IXmlTVBuilder, XmlTVBuilder>();
            serviceCollection.AddSingleton<IFileService, XMLFileService<XDocument>>();
            serviceCollection.AddSingleton<IChannelMapLoader, ChannelMapLoader>();
            serviceCollection.AddTransient<IGuideParser, GuideOneParser>();
            serviceCollection.AddTransient<IGuideParser, GuideTwoParser>();
            serviceCollection.AddTransient<IGuideParser, GuideThreeParser>();

            var argumentParser = serviceCollection.BuildServiceProvider().GetService<IAppArguments>();
            if (argumentParser == null)
            {
                Console.WriteLine("Failed to resolve IAppArguments service.");
                return;
            }

            var arguments = argumentParser.ParseArguments(args);

            if (arguments.HelpSet)
            {
                Console.WriteLine(arguments.HelpText);
                return;
            }

            if (arguments.Fake)
            {
                arguments.Urls = arguments.Fake && arguments.Urls.Count == 0
                    ? new List<string> { Path.Combine(Directory.GetCurrentDirectory(), "src", "TestData", "tvguide.json") }
                    : arguments.Urls;

                serviceCollection.AddSingleton<IDataFetcher, FakeDataFetcher>();
            }
            else
            {
                if (arguments.Urls.Count == 0)
                {
                    Console.WriteLine("No URLs provided for EPG generation.");
                    return;
                }
                serviceCollection.AddSingleton<IDataFetcher, DataFetcher>();
            }

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var dataFetcherService = serviceProvider.GetService<IDataFetcher>();
            if (dataFetcherService == null)
            {
                Console.WriteLine("Failed to resolve IDataFetcher service.");
                return;
            }

            var xmlTVBuilderService = serviceProvider.GetService<IXmlTVBuilder>();
            if (xmlTVBuilderService == null)
            {
                Console.WriteLine("Failed to resolve IXmlTVBuilder service.");
                return;
            }

            var data = await dataFetcherService.FetchDataAsync(arguments.Urls);
            if (data == null)
            {
                Console.WriteLine("Failed to fetch data.");
                return;
            }

            xmlTVBuilderService.BuildXmlTV(data, arguments.ChannelMapPath, arguments.OutputPath);
            Console.WriteLine("XML guide.xml has been generated successfully.");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message ?? "Unknown error"}");
            Environment.Exit(1);
        }
    }

    public static async Task RunEpgGenerationForWeb(string[] args)
    {
        try
        {
            Console.WriteLine("Starting XMLTV Guide Generator...");
            Console.WriteLine($"EPG_URL_FILES: {Environment.GetEnvironmentVariable("EPG_URL_FILES")}");
            Console.WriteLine($"EPG_URL: {Environment.GetEnvironmentVariable("EPG_URL")}");
            Console.WriteLine($"CHANNEL_MAP_PATH: {Environment.GetEnvironmentVariable("CHANNEL_MAP_PATH")}");
            Console.WriteLine($"OUTPUT_PATH: {Environment.GetEnvironmentVariable("OUTPUT_PATH")}");

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IAppArguments, ArgumentParser>();
            serviceCollection.AddSingleton<IXmlTVBuilder, XmlTVBuilder>();
            serviceCollection.AddSingleton<IFileService, XMLFileService<XDocument>>();
            serviceCollection.AddSingleton<IChannelMapLoader, ChannelMapLoader>();
            serviceCollection.AddTransient<IGuideParser, GuideOneParser>();
            serviceCollection.AddTransient<IGuideParser, GuideTwoParser>();
            serviceCollection.AddTransient<IGuideParser, GuideThreeParser>();

            var argumentParser = serviceCollection.BuildServiceProvider().GetService<IAppArguments>();
            if (argumentParser == null)
            {
                Console.WriteLine("Failed to resolve IAppArguments service.");
                return;
            }

            var arguments = argumentParser.ParseArguments(args);

            if (arguments.HelpSet)
            {
                Console.WriteLine(arguments.HelpText);
                return;
            }

            if (arguments.Fake)
            {
                arguments.Urls = arguments.Fake && arguments.Urls.Count == 0
                    ? new List<string> { Path.Combine(Directory.GetCurrentDirectory(), "src", "TestData", "tvguide.json") }
                    : arguments.Urls;

                serviceCollection.AddSingleton<IDataFetcher, FakeDataFetcher>();
            }
            else
            {
                if (arguments.Urls.Count == 0)
                {
                    Console.WriteLine("No URLs provided for EPG generation.");
                    return;
                }
                serviceCollection.AddSingleton<IDataFetcher, DataFetcher>();
            }

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var dataFetcherService = serviceProvider.GetService<IDataFetcher>();
            if (dataFetcherService == null)
            {
                Console.WriteLine("Failed to resolve IDataFetcher service.");
                return;
            }

            var xmlTVBuilderService = serviceProvider.GetService<IXmlTVBuilder>();
            if (xmlTVBuilderService == null)
            {
                Console.WriteLine("Failed to resolve IXmlTVBuilder service.");
                return;
            }

            var data = await dataFetcherService.FetchDataAsync(arguments.Urls);
            if (data == null)
            {
                Console.WriteLine("Failed to fetch data.");
                return;
            }

            xmlTVBuilderService.BuildXmlTV(data, arguments.ChannelMapPath, arguments.OutputPath);
            Console.WriteLine("XML guide.xml has been generated successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message ?? "Unknown error"}");
            throw; // Re-throw so the controller can handle it
        }
    }
}
