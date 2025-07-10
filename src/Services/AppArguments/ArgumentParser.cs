
using System;
using System.IO;
using System.Linq;
using xmlTVGuide.Models;

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
    --output=<path>      Specify the output path for the generated XML file.
    --help               Display this help message.";
    
    private const string EpgUrlEnv = "EPG_URL";
    private const string ChannelMapPathEnv = "CHANNEL_MAP_PATH";
    private const string OutputPathEnv = "OUTPUT_PATH";

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
        {
            DisplayHelp();
            return new ParsedArguments { HelpSet = true };
        }

        var fake = args.Contains("--fake");
        var url = GetArgumentValue(args, "--url=", EpgUrlEnv, string.Empty);
        var channelMapPath = GetArgumentValue(args, "--channelmap=", ChannelMapPathEnv, string.Empty);
        var outputPath = GetArgumentValue(args, "--output=", OutputPathEnv, Path.Combine(Directory.GetCurrentDirectory(), "output", "guide.xml"));


        ValidateArguments(url, channelMapPath, outputPath);

        return new ParsedArguments
        {
            Fake = fake,
            Urls = url.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
            ChannelMapPath = channelMapPath,
            OutputPath = outputPath
        };
    }


    /// <summary>
    /// Retrieves the value of an argument from the command line arguments or environment variables.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <param name="prefix">The prefix to look for in the arguments.</param>
    /// <param name="envVariable">The name of the environment variable to check if the argument is not found.</param>
    /// <param name="defaultValue">The default value to return if the argument is not found.</param>
    /// <returns>The value of the argument, environment variable, or default value.</returns>
    private string GetArgumentValue(string[] args, string prefix, string envVariable = null, string defaultValue = "")
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

    /// <summary>
    /// Displays the help message to the console.
    /// </summary>
    private void DisplayHelp()
    {
        Console.WriteLine(HelpMessage);
    }
}
