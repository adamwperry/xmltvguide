using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using xmlTVGuide.Models;

namespace xmlTVGuide.Services.ChannelMap;

/// <summary>
/// This class is responsible for loading the channel map from a JSON file.
/// It parses the JSON structure and extracts channel names and IDs.
/// </summary>
public class ChannelMapLoader : IChannelMapLoader
{
    private const string ChannelsKey = "channels";

    /// <summary>
    /// Loads the channel map from a JSON file.
    /// It reads the file, parses the JSON content, and extracts channel names and IDs.
    /// </summary>
    /// <param name="filePath">The path to the JSON file containing the channel map.</param>
    /// <returns>Returns a list of ChannelMapDto objects containing channel names and IDs.</returns>
    public List<ChannelMapDto> LoadChannelMap(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"The file '{filePath}' does not exist.");

        var content = File.ReadAllText(filePath);
        var root = JsonNode.Parse(content)?.AsObject() ?? throw new InvalidOperationException("Invalid JSON structure.");
        var array = root[ChannelsKey]?.AsArray() ?? throw new InvalidOperationException($"Missing '{ChannelsKey}' in channel map.");

        return array
            .Select(n => new ChannelMapDto
            {
                Name = n?["channel"]?["name"]?.ToString(),
                ChannelId = n?["channel"]?["channelId"]?.ToString()
            })
            .Where(dto => !string.IsNullOrWhiteSpace(dto.Name) && !string.IsNullOrWhiteSpace(dto.ChannelId))
            .ToList();
    }
}
