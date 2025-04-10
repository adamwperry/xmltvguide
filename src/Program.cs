using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using xmlTVGuide.Services;
using xmlTVGuide.Services.ArgumentParser;
using xmlTVGuide.Services.FileServices;
using System.Xml.Linq;
using xmlTVGuide.Services.ChannelMap;

namespace xmlTVGuide;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IAppArguments, ArgumentParser>();
            serviceCollection.AddSingleton<IXmlTVBuilder, XmlTVBuilder>();
            serviceCollection.AddSingleton<IFileService, XMLFileService<XDocument>>();
            serviceCollection.AddSingleton<IChannelMapLoader, ChannelMapLoader>();

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


            IDataFetcher dataFetcherService = null;
            if (arguments.Fake)
            {

                arguments.Url = arguments.Fake && arguments.Url.Length == 0 ? 
                    Path.Combine(Directory.GetCurrentDirectory(), "src", "TestData", "tvguide.json") 
                    : arguments.Url;

                serviceCollection.AddSingleton<IDataFetcher, FakeDataFetcher>();
            }
            else
            {
                if(arguments.Url.Length == 0)
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

            var data = await dataFetcherService.FetchDataAsync(arguments.Url);

            JsonObject epgData;
            try 
            {
                epgData = JsonNode.Parse(data)?.AsObject()
                    ?? throw new Exception("Invalid JSON structure");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Failed to parse JSON data: {ex.Message}");
                Environment.Exit(1);
                return;
            }
  
            xmlTVBuilderService.BuildXmlTV(epgData, arguments.ChannelMapPath, arguments.OutputPath);
            Console.WriteLine("XML guide.xml has been generated successfully.");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
