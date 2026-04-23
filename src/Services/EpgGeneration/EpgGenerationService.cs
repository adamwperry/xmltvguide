namespace xmlTVGuide.Services;

using xmlTVGuide.Services.ArgumentParser;
using xmlTVGuide.Services.ChannelMap;
using xmlTVGuide.Services.XMXTVBuilder;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;

public interface IEpgGenerationService
{
    Task<EpgGenerationResult> GenerateAsync(string[] args);
}

public class EpgGenerationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public Exception? Exception { get; set; }
}

public class EpgGenerationService : IEpgGenerationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EpgGenerationService> _logger;

    public EpgGenerationService(IServiceProvider serviceProvider, ILogger<EpgGenerationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<EpgGenerationResult> GenerateAsync(string[] args)
    {
        try
        {
            _logger.LogInformation("Starting XMLTV Guide Generator...");
            _logger.LogInformation($"EPG_URL_FILES: {Environment.GetEnvironmentVariable("EPG_URL_FILES")}");
            _logger.LogInformation($"CHANNEL_MAP_PATH: {Environment.GetEnvironmentVariable("CHANNEL_MAP_PATH")}");
            _logger.LogInformation($"OUTPUT_PATH: {Environment.GetEnvironmentVariable("OUTPUT_PATH")}");

            var argumentParser = _serviceProvider.GetService<IAppArguments>();
            if (argumentParser == null)
                return new EpgGenerationResult { Success = false, Message = "Failed to resolve IAppArguments service." };

            var arguments = argumentParser.ParseArguments(args);

            if (arguments.HelpSet)
                return new EpgGenerationResult { Success = true, Message = arguments.HelpText };

            if (arguments.Fake)
                arguments.Urls = arguments.Urls.Count == 0
                    ? new List<string> { Path.Combine(Directory.GetCurrentDirectory(), "src", "TestData", "tvguide.json") }
                    : arguments.Urls;
            else if (arguments.Urls.Count == 0)
                return new EpgGenerationResult { Success = false, Message = "No URLs provided for EPG generation." };

            var dataFetcher = _serviceProvider.GetService<IDataFetcher>();
            if (dataFetcher == null)
                return new EpgGenerationResult { Success = false, Message = "Failed to resolve IDataFetcher service." };

            var xmlTVBuilder = _serviceProvider.GetService<IXmlTVBuilder>();
            if (xmlTVBuilder == null)
                return new EpgGenerationResult { Success = false, Message = "Failed to resolve IXmlTVBuilder service." };

            var data = await dataFetcher.FetchDataAsync(arguments.Urls);
            if (data == null)
                return new EpgGenerationResult { Success = false, Message = "Failed to fetch data." };

            if (string.IsNullOrEmpty(arguments.ChannelMapPath) || string.IsNullOrEmpty(arguments.OutputPath))
                return new EpgGenerationResult { Success = false, Message = "Channel map path and output path are required." };

            xmlTVBuilder.BuildXmlTV(data, arguments.ChannelMapPath, arguments.OutputPath);

            var successMessage = "XML guide.xml has been generated successfully.";
            _logger.LogInformation(successMessage);
            return new EpgGenerationResult { Success = true, Message = successMessage };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during EPG generation");
            return new EpgGenerationResult
            {
                Success = false,
                Message = $"An error occurred: {ex.Message}",
                Exception = ex
            };
        }
    }
}
