using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IOFile = System.IO.File;

namespace xmlTVGuide.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly string _epgUrlsPath;
    private readonly string _channelMapPath;

    public ConfigController()
    {
        // Determine if running in Docker or locally
        var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" || 
                       Directory.Exists("/app");
        
        var basePath = isDocker ? "/app" : Directory.GetCurrentDirectory();
        
        _epgUrlsPath = Path.GetFullPath(
            Environment.GetEnvironmentVariable("EPG_URL_FILES") ??
            Path.Combine(basePath, "epg_urls.txt")
        );
        _channelMapPath = Path.GetFullPath(
            Environment.GetEnvironmentVariable("CHANNEL_MAP_PATH") ??
            Path.Combine(basePath, "ChannelMap.json")
        );
    }

    [HttpGet("epg-urls")]
    public async Task<IActionResult> GetEpgUrls()
    {
        try
        {
            if (!IOFile.Exists(_epgUrlsPath))
                return Ok(new { content = "", path = _epgUrlsPath });

            var content = await IOFile.ReadAllTextAsync(_epgUrlsPath);
            return Ok(new { content, path = _epgUrlsPath });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("epg-urls")]
    public async Task<IActionResult> SaveEpgUrls([FromBody] SaveFileRequest request)
    {
        try
        {
            // Validate URLs
            var lines = request.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var invalidUrls = new List<string>();

            foreach (var line in lines)
            {
                var url = line.Trim();
                if (!string.IsNullOrEmpty(url) && !Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    invalidUrls.Add(url);
                }
            }

            if (invalidUrls.Any())
            {
                return BadRequest(new { error = "Invalid URLs found", invalidUrls });
            }

            var normalizedContent = request.Content.Replace("\r\n", "\n");
            await IOFile.WriteAllTextAsync(_epgUrlsPath, normalizedContent);
            return Ok(new { message = "EPG URLs saved successfully", path = _epgUrlsPath });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("channel-map")]
    public async Task<IActionResult> GetChannelMap()
    {
        try
        {
            if (!IOFile.Exists(_channelMapPath))
                return Ok(new { content = "{\"channels\": []}", path = _channelMapPath });

            var content = await IOFile.ReadAllTextAsync(_channelMapPath);
            return Ok(new { content, path = _channelMapPath });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("channel-map")]
    public async Task<IActionResult> SaveChannelMap([FromBody] SaveFileRequest request)
    {
        try
        {
            // Validate JSON
            JObject.Parse(request.Content);

            var normalizedContent = request.Content.Replace("\r\n", "\n");
            await IOFile.WriteAllTextAsync(_channelMapPath, normalizedContent);
            return Ok(new { message = "Channel map saved successfully", path = _channelMapPath });
        }
        catch (JsonException ex)
        {
            return BadRequest(new { error = "Invalid JSON format", details = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("validate-json")]
    public IActionResult ValidateJson([FromBody] SaveFileRequest request)
    {
        try
        {
            JObject.Parse(request.Content);
            return Ok(new { valid = true, message = "Valid JSON" });
        }
        catch (JsonException ex)
        {
            return Ok(new { valid = false, error = ex.Message });
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var status = new
        {
            epgUrlsExists = IOFile.Exists(_epgUrlsPath),
            channelMapExists = IOFile.Exists(_channelMapPath),
            epgUrlsPath = _epgUrlsPath,
            channelMapPath = _channelMapPath,
            lastModified = new
            {
                epgUrls = IOFile.Exists(_epgUrlsPath) ? IOFile.GetLastWriteTime(_epgUrlsPath) : (DateTime?)null,
                channelMap = IOFile.Exists(_channelMapPath) ? IOFile.GetLastWriteTime(_channelMapPath) : (DateTime?)null
            }
        };

        return Ok(status);
    }
}

public class SaveFileRequest
{
    public string Content { get; set; } = "";
}