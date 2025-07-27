using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using xmlTVGuide.Services;
using xmlTVGuide.Services.ArgumentParser;
using xmlTVGuide.Services.FileServices;
using System.Xml.Linq;
using xmlTVGuide.Services.ChannelMap;
using System.Collections.Generic;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;

namespace xmlTVGuide;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
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
                Environment.Exit(1);
                return;
            }

            var arguments = argumentParser.ParseArguments(args);

            if (arguments.HelpSet)
                return;


            IDataFetcher? dataFetcherService = null;
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
                    Console.WriteLine("Please provide a URL using --url=<url>.");
                    return;
                }
                serviceCollection.AddSingleton<IDataFetcher, DataFetcher>();
            }

            var serviceProvider = serviceCollection.BuildServiceProvider();
            dataFetcherService = serviceProvider.GetService<IDataFetcher>() 
                ?? throw new InvalidOperationException("Failed to resolve IDataFetcher service.");

            if (dataFetcherService == null)
            {
                Console.WriteLine("Failed to resolve IDataFetcher service.");
                Environment.Exit(1);
                return;
            }

            var xmlTVBuilderService = serviceProvider.GetService<IXmlTVBuilder>();
            if (xmlTVBuilderService == null)
            {
                Console.WriteLine("Failed to resolve IXmlTVBuilder service.");
                Environment.Exit(1);
                return;
            }

            var data = await dataFetcherService.FetchDataAsync(arguments.Urls);
            if (data == null)
            {
                Console.WriteLine("Failed to fetch data.");
                Environment.Exit(1);
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
}
