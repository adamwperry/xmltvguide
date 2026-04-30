using System.Text.Json.Nodes;

namespace xmlTVGuide.Services.Validation;

public interface IValidationService
{
    /// <summary>
    /// Tests if a URL is reachable, returns valid JSON, and is in a supported format.
    /// </summary>
    Task<SourceTestResult> TestSourceAsync(string url);

    /// <summary>
    /// Previews channels that would be detected from a source with the given channel map.
    /// </summary>
    Task<ChannelPreviewResult> PreviewChannelsAsync(string url, string? channelMapPath);
}

public class SourceTestResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public SourceReachability Reachability { get; set; } = new();
    public FormatValidation Format { get; set; } = new();
    public List<string> SupportedParsers { get; set; } = new();
}

public class SourceReachability
{
    public bool IsReachable { get; set; }
    public int? HttpStatusCode { get; set; }
    public long? ResponseSizeBytes { get; set; }
    public int? ResponseTimeMs { get; set; }
    public string? Error { get; set; }
}

public class FormatValidation
{
    public bool IsValidJson { get; set; }
    public string? JsonError { get; set; }
    public bool HasRecognizedStructure { get; set; }
    public List<string> RecognizedFormats { get; set; } = new();
}

public class ChannelPreviewResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<PreviewedChannel> DetectedChannels { get; set; } = new();
    public int TotalChannels { get; set; }
    public int MappedChannels { get; set; }
    public int UnmappedChannels { get; set; }
}

public class PreviewedChannel
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? MappedName { get; set; }
    public bool IsMapped { get; set; }
    public int ProgramCount { get; set; }
    public List<string> Sources { get; set; } = new();
}
