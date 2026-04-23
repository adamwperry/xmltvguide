using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using xmlTVGuide.Services.ChannelMap;
using xmlTVGuide.Services.XMXTVBuilder.Parsers;
using xmlTVGuide.Utilities;

namespace xmlTVGuide.Services.Validation;

public class ValidationService : IValidationService
{
    private readonly IDataFetcher _dataFetcher;
    private readonly IEnumerable<IGuideParser> _parsers;
    private readonly IChannelMapLoader _channelMapLoader;
    private readonly ILogger<ValidationService> _logger;
    private const int TimeoutMs = 15000; // 15 second timeout for fetching sources
    private const string UnixTimePlaceholder = "{unixtime}";
    private const string YearMonthPlaceholder = "{monthyear}";
    private const string YearMonthPlaceholderAlt = "{yearmonth}"; // legacy placeholder name

    public ValidationService(
        IDataFetcher dataFetcher,
        IEnumerable<IGuideParser> parsers,
        IChannelMapLoader channelMapLoader,
        ILogger<ValidationService> logger)
    {
        _dataFetcher = dataFetcher;
        _parsers = parsers;
        _channelMapLoader = channelMapLoader;
        _logger = logger;
    }

    public async Task<SourceTestResult> TestSourceAsync(string url)
    {
        var result = new SourceTestResult();

        if (string.IsNullOrWhiteSpace(url))
        {
            result.Message = "URL cannot be empty";
            return result;
        }

        // Test reachability
        var (isReachable, statusCode, responseSize, responseTime, error) = await TestReachability(url);
        result.Reachability.IsReachable = isReachable;
        result.Reachability.HttpStatusCode = statusCode;
        result.Reachability.ResponseSizeBytes = responseSize;
        result.Reachability.ResponseTimeMs = responseTime;
        result.Reachability.Error = error;

        if (!isReachable)
        {
            result.Message = $"Source is not reachable: {error}";
            return result;
        }

        // Fetch and validate format
        try
        {
            var data = await _dataFetcher.FetchDataAsync(url);

            if (string.IsNullOrEmpty(data))
            {
                result.Message = "Source returned empty response";
                return result;
            }

            // Parse JSON
            JsonObject? jsonObject = null;
            try
            {
                var jsonDoc = JsonDocument.Parse(data);
                jsonObject = JsonSerializer.Deserialize<JsonObject>(data);
                result.Format.IsValidJson = true;
            }
            catch (JsonException ex)
            {
                result.Format.IsValidJson = false;
                result.Format.JsonError = ex.Message;
                result.Message = "Response is not valid JSON";
                return result;
            }

            if (jsonObject == null)
            {
                result.Message = "Failed to parse JSON response";
                return result;
            }

            // Test which parsers can handle this format
            var supportedParsers = new List<string>();
            foreach (var parser in _parsers)
            {
                try
                {
                    if (parser.CanParse(jsonObject))
                    {
                        var parserName = parser.GetType().Name;
                        supportedParsers.Add(parserName);
                        result.Format.RecognizedFormats.Add(parserName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking parser {ParserName}", parser.GetType().Name);
                }
            }

            if (!supportedParsers.Any())
            {
                result.Format.HasRecognizedStructure = false;
                result.Message = "Source format is not recognized by any parser";
                return result;
            }

            result.Format.HasRecognizedStructure = true;
            result.SupportedParsers = supportedParsers;
            result.Success = true;
            result.Message = $"Source is operational. Supports: {string.Join(", ", supportedParsers)}";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing source {Url}", url);
            result.Message = $"Error testing source: {ex.Message}";
            return result;
        }
    }

    public async Task<ChannelPreviewResult> PreviewChannelsAsync(string url, string? channelMapPath)
    {
        var result = new ChannelPreviewResult();

        try
        {
            var data = await _dataFetcher.FetchDataAsync(url);

            if (string.IsNullOrEmpty(data))
            {
                result.Message = "Source returned empty response";
                return result;
            }

            // Parse JSON
            JsonObject? jsonObject = null;
            try
            {
                jsonObject = JsonSerializer.Deserialize<JsonObject>(data);
            }
            catch (JsonException ex)
            {
                result.Message = $"Invalid JSON: {ex.Message}";
                return result;
            }

            if (jsonObject == null)
            {
                result.Message = "Failed to parse JSON";
                return result;
            }

            // Load channel map if provided
            List<Models.ChannelMapDto>? channelMap = null;
            if (!string.IsNullOrEmpty(channelMapPath))
            {
                try
                {
                    channelMap = _channelMapLoader.LoadChannelMap(channelMapPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load channel map from {Path}", channelMapPath);
                }
            }

            // Find which parser can handle this
            IGuideParser? matchingParser = null;
            foreach (var parser in _parsers)
            {
                try
                {
                    if (parser.CanParse(jsonObject))
                    {
                        matchingParser = parser;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking parser {ParserName}", parser.GetType().Name);
                }
            }

            if (matchingParser == null)
            {
                result.Message = "No parser can handle this source format";
                return result;
            }

            // Extract channels
            var channels = ExtractChannels(jsonObject, channelMap);
            result.DetectedChannels = channels;
            result.TotalChannels = channels.Count;
            result.MappedChannels = channels.Count(c => c.IsMapped);
            result.UnmappedChannels = channels.Count(c => !c.IsMapped);
            result.Success = true;
            result.Message = $"Detected {result.TotalChannels} channels ({result.MappedChannels} mapped)";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing channels from {Url}", url);
            result.Message = $"Error previewing channels: {ex.Message}";
            return result;
        }
    }

    private async Task<(bool isReachable, int? statusCode, long? responseSize, int? responseTime, string? error)> TestReachability(string url)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Replace placeholders before testing
            url = ReplacePlaceholders(url);

            using var client = DataFetcherBase.GetClientAsync(UserAgent.Chrome);
            client.Timeout = TimeSpan.FromMilliseconds(TimeoutMs);
            
            using var response = await client.GetAsync(url);

            sw.Stop();
            var content = await response.Content.ReadAsStringAsync();
            var contentLength = (long)content.Length;

            if (response.IsSuccessStatusCode)
                return (true, (int)response.StatusCode, contentLength, (int)sw.ElapsedMilliseconds, null);

            return (false, (int)response.StatusCode, contentLength, (int)sw.ElapsedMilliseconds, $"HTTP {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return (false, null, null, (int)sw.ElapsedMilliseconds, ex.Message);
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return (false, null, null, (int)sw.ElapsedMilliseconds, $"Timeout after {TimeoutMs}ms");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, null, null, (int)sw.ElapsedMilliseconds, ex.Message);
        }
    }

    private string ReplacePlaceholders(string url)
    {
        if (url.Contains(UnixTimePlaceholder))
        {
            var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            url = url.Replace(UnixTimePlaceholder, unixTime.ToString());
        }

        if (url.Contains(YearMonthPlaceholder))
        {
            var now = DateTime.UtcNow;
            var yearMonth = now.ToString("yyyy-MM");
            url = url.Replace(YearMonthPlaceholder, yearMonth);
        }

        // Also handle legacy placeholder name for backward compatibility
        if (url.Contains(YearMonthPlaceholderAlt))
        {
            var now = DateTime.UtcNow;
            var yearMonth = now.ToString("yyyy-MM");
            url = url.Replace(YearMonthPlaceholderAlt, yearMonth);
        }

        return url;
    }

    private List<PreviewedChannel> ExtractChannels(JsonObject epgData, List<Models.ChannelMapDto>? channelMap)
    {
        var channels = new List<PreviewedChannel>();

        try
        {
            // Try to extract channels from common structures
            if (epgData.TryGetPropertyValue("channels", out var channelsNode) && channelsNode is JsonArray channelArray)
            {
                foreach (var ch in channelArray)
                {
                    if (ch is JsonObject channelObj)
                    {
                        var id = channelObj["channelId"]?.GetValue<string>() ??
                                channelObj["channel_id"]?.GetValue<string>() ??
                                channelObj["id"]?.GetValue<string>() ?? "";

                        var displayName = channelObj["callSign"]?.GetValue<string>() ??
                                        channelObj["call_sign"]?.GetValue<string>() ??
                                        channelObj["name"]?.GetValue<string>() ?? id;

                        var programCount = 0;
                        if (channelObj.TryGetPropertyValue("events", out var eventsNode) && eventsNode is JsonArray eventsArray)
                            programCount = eventsArray.Count;
                        else if (channelObj.TryGetPropertyValue("programs", out var programsNode) && programsNode is JsonArray programsArray)
                            programCount = programsArray.Count;

                        var mappedName = channelMap?.FirstOrDefault(m =>
                            m.ChannelId == id)?.Name;

                        channels.Add(new PreviewedChannel
                        {
                            Id = id,
                            DisplayName = displayName,
                            MappedName = mappedName,
                            IsMapped = !string.IsNullOrEmpty(mappedName),
                            ProgramCount = programCount
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting channels from EPG data");
        }

        return channels;
    }
}
