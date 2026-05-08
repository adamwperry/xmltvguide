using xmlTVGuide.Services;
using xmlTVGuide.Services.ArgumentParser;
using xmlTVGuide.Services.FileServices;
using xmlTVGuide.Services.ChannelMap;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;

namespace xmlTVGuide;

class Program
{
    static async Task Main(string[] args)
    {
        // Check if we should run as web host
        var runAsWeb = string.Equals(
            Environment.GetEnvironmentVariable("RUN_AS_WEB"),
            "true",
            StringComparison.OrdinalIgnoreCase);

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

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        // Determine port based on environment
        var port = Environment.GetEnvironmentVariable("PORT") ?? GetDefaultPort();

        // Determine wwwroot path based on environment
        var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
                       Directory.Exists("/app");
        var wwwrootPath = isDocker
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : Path.Combine(Directory.GetCurrentDirectory(), "src", "wwwroot");

        return Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
                webBuilder.UseUrls($"http://0.0.0.0:{port}");
                webBuilder.UseWebRoot(wwwrootPath);
            });
    }

    private static string GetDefaultPort()
    {
        // Check if running in Docker
        var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
                       Directory.Exists("/app");

        return isDocker ? "80" : "8585";
    }

    public static async Task RunEpgGeneration(string[] args)
    {
        var result = await RunEpgGenerationInternal(args);

        if (!result.Success)
        {
            Console.WriteLine(result.Message);
            Environment.Exit(1);
        }
        else
        {
            Console.WriteLine(result.Message);
            Environment.Exit(0);
        }
    }

    public static async Task<EpgGenerationResult> RunEpgGenerationForWeb(string[] args)
    {
        return await RunEpgGenerationInternal(args);
    }

    private static async Task<EpgGenerationResult> RunEpgGenerationInternal(string[] args)
    {
        var serviceCollection = new ServiceCollection();

        // Register core services
        serviceCollection.AddLogging(config => config.AddConsole());
        serviceCollection.AddSingleton<IAppArguments, ArgumentParser>();
        serviceCollection.AddSingleton<IXmlTVBuilder, XmlTVBuilder>();
        serviceCollection.AddSingleton<IFileService, XMLFileService>();
        serviceCollection.AddSingleton<IChannelMapLoader, ChannelMapLoader>();
        serviceCollection.AddSingleton<IEpgGenerationStatusTracker, InMemoryEpgGenerationStatusTracker>();
        serviceCollection.AddSingleton<IEpgGenerationService, EpgGenerationService>();
        serviceCollection.AddTransient<IGuideParser, GuideOneParser>();
        serviceCollection.AddTransient<IGuideParser, GuideTwoParser>();
        serviceCollection.AddTransient<IGuideParser, GuideThreeParser>();

        // Check for fake data before creating final service provider
        var argumentParser = serviceCollection.BuildServiceProvider().GetService<IAppArguments>();
        if (argumentParser == null)
            return new EpgGenerationResult { Success = false, Message = "Failed to resolve IAppArguments service." };

        var arguments = argumentParser.ParseArguments(args);

        // Add appropriate data fetcher based on fake flag
        if (arguments.Fake)
        {
            arguments.Urls = arguments.Urls.Count == 0
                ? new List<string> { Path.Combine(Directory.GetCurrentDirectory(), "src", "TestData", "tvguide.json") }
                : arguments.Urls;
            serviceCollection.AddSingleton<IDataFetcher, FakeDataFetcher>();
        }
        else
            serviceCollection.AddSingleton<IDataFetcher, DataFetcher>();

        // Create final service provider with all services
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var generationService = serviceProvider.GetService<IEpgGenerationService>();

        if (generationService == null)
            return new EpgGenerationResult { Success = false, Message = "Failed to resolve IEpgGenerationService." };

        return await generationService.GenerateAsync(args);
    }
}
