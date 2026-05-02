using xmlTVGuide.Models;
using System.Text.Json;
using AppSettingsModel = xmlTVGuide.Services.AppSettings.AppSettings;
using ChannelOutputSettingsModel = xmlTVGuide.Services.AppSettings.ChannelOutputSettings;

namespace xmlTVGuide.Services.ArgumentParser;

/// <summary>
/// This class implements the IAppArguments interface to parse command line arguments.
/// It provides methods to retrieve the values of the arguments and validate them.
/// The class also includes a help message that describes the usage of the application.
/// </summary>
public class ArgumentParser : IAppArguments
{
    private const string HelpMessage = @"
    Usage:
    --fake               Use fake data for testing.
    --channelmap=<path>  Specify the path to the channel map JSON file.
    --url=<url>          Specify the URL or file path for the data source.
    --epgUrlFiles=<path> Specify the path to the EPG URLs file.
    --output=<path>      Specify the output path for the generated XML file.
    --strip-channel-numbers
                         Remove leading channel numbers from display names.
    --preserve-channel-order
                         Keep provider/parser channel order instead of sorting by channel ID.
    --help               Display this help message.";

    private const string EpgUrlEnv = "EPG_URL";
    private const string EpgUrlFilesEnv = "EPG_URL_FILES";
    private const string ChannelMapPathEnv = "CHANNEL_MAP_PATH";
    private const string OutputPathEnv = "OUTPUT_PATH";
    private const string SettingsPathEnv = "SETTINGS_PATH";
    private const string UseChannelNamesEnv = "USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS";
    private const string StripChannelNumbersEnv = "STRIP_CHANNEL_NUMBERS";
    private const string SortChannelsEnv = "SORT_CHANNELS_BY_ID";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Parses the command line arguments and returns a ParsedArguments object.
    /// It checks for the presence of the --help argument and displays the help message if found.
    /// It retrieves the values of the --url, --channelmap, and --output arguments, 
    /// </summary>
    /// <param name="args">The command line arguments passed to the application.</param>
    /// <returns>Returns a ParsedArguments object containing the parsed values.</returns>
    public ParsedArguments ParseArguments(string[] args)
    {
        if (args.Contains("--help"))
            return new ParsedArguments { HelpSet = true, HelpText = HelpMessage };

        var fake = args.Contains("--fake");

        var urlArg = SetUrlVariable(args);
        var url = string.IsNullOrEmpty(urlArg) ? GetArgumentValue(args, "--url=", EpgUrlEnv, string.Empty) : urlArg;

        var channelMapPath = GetArgumentValue(args, "--channelmap=", ChannelMapPathEnv, string.Empty);
        var outputPath = GetArgumentValue(args, "--output=", OutputPathEnv, Path.Combine(Directory.GetCurrentDirectory(), "output", "guide.xml"));
        var settings = LoadAppSettings();
        var stripChannelNumbers = GetBooleanFlag(
            args,
            "--strip-channel-numbers",
            settings.Channel.UseChannelNamesInsteadOfNumericIds,
            UseChannelNamesEnv,
            StripChannelNumbersEnv);
        var sortChannels = GetBooleanArgument(
            args,
            "--sort-channels-by-id",
            "--preserve-channel-order",
            settings.Channel.SortChannelsByIdThenDisplayName,
            SortChannelsEnv);

        ValidateArguments(url, channelMapPath, outputPath);

        return new ParsedArguments
        {
            Fake = fake,
            Urls = url.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
            ChannelMapPath = channelMapPath,
            OutputPath = outputPath,
            StripChannelNumbers = stripChannelNumbers,
            SortChannelsByIdThenDisplayName = sortChannels
        };
    }

    /// <summary>
    /// Sets the URL variable by checking command line arguments and environment variables.
    /// It prioritizes the --url argument over the --epgUrlFiles argument.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The selected URL.</returns>
    private string SetUrlVariable(string[] args)
    {
        var urls = GetArgumentValue(args, "--url=", EpgUrlEnv, string.Empty);
        var urlFiles = GetArgumentValue(args, "--epgUrlFiles=", EpgUrlFilesEnv, string.Empty);

        if (!string.IsNullOrEmpty(urls))
            return urls;

        if (!string.IsNullOrEmpty(urlFiles))
        {
            if (!File.Exists(urlFiles))
            {
                Console.WriteLine($"The specified EPG URL file does not exist: {urlFiles}");
                return string.Empty;
            }

            try
            {
                var fileUrls = File.ReadAllLines(urlFiles)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith("#")); // Ignore empty lines and comments

                return string.Join(",", fileUrls);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading EPG URL file: {ex.Message}");
                return string.Empty;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Retrieves the value of an argument from the command line arguments or environment variables.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <param name="prefix">The prefix to look for in the arguments.</param>
    /// <param name="envVariable">The name of the environment variable to check if the argument is not found.</param>
    /// <param name="defaultValue">The default value to return if the argument is not found.</param>
    /// <returns>The value of the argument, environment variable, or default value.</returns>
    private string GetArgumentValue(string[] args, string prefix, string? envVariable = null, string defaultValue = "")
    {
        var arg = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(arg))
            return arg.Substring(prefix.Length);

        if (!string.IsNullOrEmpty(envVariable))
        {
            var envValue = Environment.GetEnvironmentVariable(envVariable);
            if (!string.IsNullOrEmpty(envValue))
                return envValue;
        }

        return defaultValue;
    }

    private static AppSettingsModel LoadAppSettings()
    {
        var settingsPath = GetSettingsPath();
        if (!File.Exists(settingsPath))
            return new AppSettingsModel();

        try
        {
            var content = File.ReadAllText(settingsPath);
            var settings = string.IsNullOrWhiteSpace(content)
                ? new AppSettingsModel()
                : JsonSerializer.Deserialize<AppSettingsModel>(content, JsonOptions) ?? new AppSettingsModel();

            settings.Channel ??= new ChannelOutputSettingsModel();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Warning: Could not read settings file '{settingsPath}': {ex.Message}");
            return new AppSettingsModel();
        }
    }

    private static string GetSettingsPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable(SettingsPathEnv);
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath);

        var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
                       Directory.Exists("/app");
        var basePath = isDocker ? "/app" : Directory.GetCurrentDirectory();

        return Path.Combine(basePath, "settings.json");
    }

    private static bool GetBooleanFlag(string[] args, string flag, bool defaultValue, params string[] envVariables)
    {
        if (args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)))
            return true;

        return GetEnvironmentBoolean(envVariables) ?? defaultValue;
    }

    private static bool GetBooleanArgument(string[] args, string trueFlag, string falseFlag, bool defaultValue, params string[] envVariables)
    {
        if (args.Any(a => string.Equals(a, trueFlag, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (args.Any(a => string.Equals(a, falseFlag, StringComparison.OrdinalIgnoreCase)))
            return false;

        return GetEnvironmentBoolean(envVariables) ?? defaultValue;
    }

    private static bool? GetEnvironmentBoolean(params string[] envVariables)
    {
        foreach (var envVariable in envVariables)
        {
            if (string.IsNullOrWhiteSpace(envVariable))
                continue;

            var envValue = Environment.GetEnvironmentVariable(envVariable);
            if (string.IsNullOrWhiteSpace(envValue))
                continue;

            if (string.Equals(envValue, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(envValue, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(envValue, "yes", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(envValue, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(envValue, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(envValue, "no", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return null;
    }

    /// <summary>
    /// Validates the command line arguments.
    /// </summary>
    /// <param name="url">The URL to be validated.</param>
    /// <param name="channelMapPath">The channel map path to be validated.</param>
    /// <param name="outputPath">The output path to be validated.</param>
    /// <exception cref="ArgumentException"></exception>
    private void ValidateArguments(string url, string channelMapPath, string outputPath)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentException("The URL (--url) must be provided or set via the EPG_URL environment variable.");

        //@todo validate Urls for one or more and the formats

        if (string.IsNullOrEmpty(channelMapPath))
            Console.WriteLine("Warning: No channel map path provided. Defaulting to an empty value.");

        if (string.IsNullOrEmpty(outputPath))
            throw new ArgumentException("The output path (--output) must be provided.");
    }
}
