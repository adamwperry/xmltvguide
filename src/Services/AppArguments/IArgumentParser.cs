using xmlTVGuide.Models;

namespace xmlTVGuide.Services.ArgumentParser;

/// <summary>
/// This interface defines the contract for parsing command line arguments.
/// It includes a method to parse the arguments and return a <see cref="ParsedArguments"/> object.
/// </summary>
public interface IAppArguments
{
    ParsedArguments ParseArguments(string[] args);
}
