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
        return AnalyzeChannelMapContent(content).ValidChannels;
    }

    public ChannelMapAnalysis AnalyzeChannelMapContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Invalid JSON structure.");

        var root = JsonNode.Parse(content)?.AsObject() ?? throw new InvalidOperationException("Invalid JSON structure.");
        var array = root[ChannelsKey]?.AsArray() ?? throw new InvalidOperationException($"Missing '{ChannelsKey}' in channel map.");

        var analysis = new ChannelMapAnalysis
        {
            TotalEntries = array.Count
        };

        var entries = array
            .Select((node, index) => new
            {
                EntryNumber = index + 1,
                Name = node?["channel"]?["name"]?.ToString()?.Trim(),
                ChannelId = node?["channel"]?["channelId"]?.ToString()?.Trim()
            })
            .ToList();

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.ChannelId))
            {
                analysis.BlankChannelIdEntries.Add(new ChannelMapEntryIssue
                {
                    EntryNumber = entry.EntryNumber,
                    Name = entry.Name,
                    ChannelId = entry.ChannelId
                });
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                analysis.BlankNameEntries.Add(new ChannelMapEntryIssue
                {
                    EntryNumber = entry.EntryNumber,
                    Name = entry.Name,
                    ChannelId = entry.ChannelId
                });
            }

            if (!string.IsNullOrWhiteSpace(entry.Name) && !string.IsNullOrWhiteSpace(entry.ChannelId))
            {
                analysis.ValidChannels.Add(new ChannelMapDto
                {
                    Name = entry.Name,
                    ChannelId = entry.ChannelId
                });
            }
        }

        analysis.ValidEntries = analysis.ValidChannels.Count;
        analysis.BlankChannelIdCount = analysis.BlankChannelIdEntries.Count;
        analysis.BlankNameCount = analysis.BlankNameEntries.Count;
        analysis.DuplicateChannelIdGroups = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ChannelId))
            .GroupBy(entry => entry.ChannelId!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new DuplicateChannelMapGroup
            {
                ChannelId = group.Key,
                EntryNumbers = group.Select(entry => entry.EntryNumber).ToList(),
                Names = group.Select(entry => entry.Name ?? "(blank)").Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            })
            .OrderBy(group => group.ChannelId)
            .ToList();
        analysis.DuplicateChannelIdCount = analysis.DuplicateChannelIdGroups.Count;

        return analysis;
    }
}
