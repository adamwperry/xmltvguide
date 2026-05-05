using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using System.Text.Json;
using xmlTVGuide.Services.AppSettings;
using xmlTVGuide.Services.ChannelMap;
using xmlTVGuide.Services.Validation;
using IOFile = System.IO.File;

namespace xmlTVGuide.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfigController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _epgUrlsPath;
    private readonly string _channelMapPath;
    private readonly IValidationService _validationService;
    private readonly IChannelMapLoader _channelMapLoader;
    private readonly IAppSettingsService _appSettingsService;

    public ConfigController(
        IValidationService validationService,
        IChannelMapLoader channelMapLoader,
        IAppSettingsService appSettingsService)
    {
        _validationService = validationService;
        _channelMapLoader = channelMapLoader;
        _appSettingsService = appSettingsService;

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
            var invalidUrls = GetInvalidEpgUrls(request.Content);
            if (invalidUrls.Any())
            {
                return BadRequest(new { error = "Invalid URLs found - must start with http:// or https://", invalidUrls });
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

    [HttpGet("backup")]
    public async Task<IActionResult> ExportBackup()
    {
        try
        {
            var backup = new ConfigBackup
            {
                ExportedAtUtc = DateTime.UtcNow,
                EpgUrls = new ConfigBackupFile
                {
                    Path = _epgUrlsPath,
                    Content = IOFile.Exists(_epgUrlsPath) ? await IOFile.ReadAllTextAsync(_epgUrlsPath) : ""
                },
                ChannelMap = new ConfigBackupFile
                {
                    Path = _channelMapPath,
                    Content = IOFile.Exists(_channelMapPath) ? await IOFile.ReadAllTextAsync(_channelMapPath) : "{\"channels\": []}"
                },
                Settings = new ConfigBackupFile
                {
                    Path = _appSettingsService.SettingsPath,
                    Content = IOFile.Exists(_appSettingsService.SettingsPath)
                        ? await IOFile.ReadAllTextAsync(_appSettingsService.SettingsPath)
                        : JsonSerializer.Serialize(new AppSettings(), JsonOptions)
                }
            };

            var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
            var fileName = $"xmltvguide-config-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            return File(Encoding.UTF8.GetBytes(json), "application/json", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("restore")]
    public async Task<IActionResult> RestoreBackup([FromBody] ConfigBackup request)
    {
        try
        {
            if (request.EpgUrls is null || request.ChannelMap is null)
                return BadRequest(new { error = "Backup must include epgUrls and channelMap sections." });

            var invalidUrls = GetInvalidEpgUrls(request.EpgUrls.Content);
            if (invalidUrls.Any())
                return BadRequest(new { error = "Invalid URLs found - must start with http:// or https://", invalidUrls });

            var analysis = _channelMapLoader.AnalyzeChannelMapContent(request.ChannelMap.Content);
            AppSettings? restoredSettings = null;

            if (request.Settings is not null)
            {
                restoredSettings = JsonSerializer.Deserialize<AppSettings>(request.Settings.Content, JsonOptions)
                    ?? new AppSettings();
                restoredSettings.Channel ??= new ChannelOutputSettings();
            }

            var pendingWrites = new List<PendingFileWrite>
            {
                new(_epgUrlsPath, request.EpgUrls.Content.Replace("\r\n", "\n")),
                new(_channelMapPath, request.ChannelMap.Content.Replace("\r\n", "\n"))
            };

            if (restoredSettings is not null)
            {
                pendingWrites.Add(new PendingFileWrite(
                    _appSettingsService.SettingsPath,
                    JsonSerializer.Serialize(restoredSettings, JsonOptions) + "\n"));
            }

            await WriteFilesWithRollbackAsync(pendingWrites);

            return Ok(new
            {
                message = "Configuration restored successfully",
                epgUrlsPath = _epgUrlsPath,
                channelMapPath = _channelMapPath,
                analysis,
                warnings = BuildChannelMapWarnings(analysis)
            });
        }
        catch (JsonException ex)
        {
            return BadRequest(new { error = "Invalid channel map JSON format", details = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
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

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        try
        {
            var settings = await _appSettingsService.LoadAsync();
            return Ok(new { settings, path = _appSettingsService.SettingsPath });
        }
        catch (JsonException ex)
        {
            return BadRequest(new { error = "Invalid settings JSON format", details = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] AppSettings request)
    {
        try
        {
            request.Channel ??= new ChannelOutputSettings();
            await _appSettingsService.SaveAsync(request);

            return Ok(new
            {
                message = "Settings saved successfully",
                settings = request,
                path = _appSettingsService.SettingsPath
            });
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
            var analysis = _channelMapLoader.AnalyzeChannelMapContent(request.Content);

            var normalizedContent = request.Content.Replace("\r\n", "\n");
            await IOFile.WriteAllTextAsync(_channelMapPath, normalizedContent);
            return Ok(new
            {
                message = "Channel map saved successfully",
                path = _channelMapPath,
                analysis,
                warnings = BuildChannelMapWarnings(analysis)
            });
        }
        catch (JsonException ex)
        {
            return BadRequest(new { error = "Invalid JSON format", details = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
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
            var analysis = _channelMapLoader.AnalyzeChannelMapContent(request.Content);
            return Ok(new
            {
                valid = true,
                message = "Valid channel map JSON",
                analysis,
                warnings = BuildChannelMapWarnings(analysis)
            });
        }
        catch (JsonException ex)
        {
            return Ok(new { valid = false, error = ex.Message });
        }
        catch (InvalidOperationException ex)
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

    [HttpPost("test-source")]
    public async Task<IActionResult> TestSource([FromBody] TestSourceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { error = "URL is required" });

        var result = await _validationService.TestSourceAsync(request.Url);
        return Ok(result);
    }

    [HttpPost("preview-channels")]
    public async Task<IActionResult> PreviewChannels([FromBody] PreviewChannelsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { error = "URL is required" });

        var channelMapPath = request.UseCurrentMap && IOFile.Exists(_channelMapPath) ? _channelMapPath : null;
        var result = await _validationService.PreviewChannelsAsync(request.Url, channelMapPath);
        return Ok(result);
    }

    private static List<string> BuildChannelMapWarnings(ChannelMapAnalysis analysis)
    {
        var warnings = new List<string>();

        if (analysis.BlankChannelIdCount > 0)
            warnings.Add($"{analysis.BlankChannelIdCount} entr{(analysis.BlankChannelIdCount == 1 ? "y has" : "ies have")} blank channelId and will be ignored.");

        if (analysis.BlankNameCount > 0)
            warnings.Add($"{analysis.BlankNameCount} entr{(analysis.BlankNameCount == 1 ? "y has" : "ies have")} blank name and will be ignored.");

        if (analysis.DuplicateChannelIdCount > 0)
        {
            var duplicateSummary = string.Join(", ",
                analysis.DuplicateChannelIdGroups
                    .Take(5)
                    .Select(group => $"{group.ChannelId} ({group.EntryNumbers.Count} entries)"));

            var suffix = analysis.DuplicateChannelIdGroups.Count > 5 ? ", ..." : "";
            warnings.Add($"Duplicate channelId values detected: {duplicateSummary}{suffix}. The first matching entry wins during mapping.");
        }

        return warnings;
    }

    private static List<string> GetInvalidEpgUrls(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var invalidUrls = new List<string>();

        foreach (var line in lines)
        {
            var url = line.Trim();
            if (string.IsNullOrEmpty(url) || url.StartsWith("#", StringComparison.Ordinal))
                continue;

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                invalidUrls.Add(url);
            }
        }

        return invalidUrls;
    }

    private static async Task WriteFilesWithRollbackAsync(IReadOnlyList<PendingFileWrite> pendingWrites)
    {
        var snapshots = new List<FileSnapshot>(pendingWrites.Count);

        foreach (var pendingWrite in pendingWrites)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pendingWrite.Path) ?? Directory.GetCurrentDirectory());
            var fileExists = IOFile.Exists(pendingWrite.Path);
            snapshots.Add(new FileSnapshot(
                pendingWrite.Path,
                fileExists,
                fileExists ? await IOFile.ReadAllTextAsync(pendingWrite.Path) : null));
        }

        try
        {
            foreach (var pendingWrite in pendingWrites)
                await IOFile.WriteAllTextAsync(pendingWrite.Path, pendingWrite.Content);
        }
        catch
        {
            await RollbackWritesAsync(snapshots);
            throw;
        }
    }

    private static async Task RollbackWritesAsync(IEnumerable<FileSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots.Reverse())
        {
            try
            {
                if (snapshot.Existed)
                    await IOFile.WriteAllTextAsync(snapshot.Path, snapshot.OriginalContent ?? "");
                else if (IOFile.Exists(snapshot.Path))
                    IOFile.Delete(snapshot.Path);
            }
            catch
            {
                // Best effort rollback to avoid masking the original restore error.
            }
        }
    }
}

public class TestSourceRequest
{
    public string Url { get; set; } = "";
}

public class PreviewChannelsRequest
{
    public string Url { get; set; } = "";
    public bool UseCurrentMap { get; set; } = true;
}

public class SaveFileRequest
{
    public string Content { get; set; } = "";
}

public class ConfigBackup
{
    public int Version { get; set; } = 1;
    public DateTime ExportedAtUtc { get; set; }
    public ConfigBackupFile? EpgUrls { get; set; }
    public ConfigBackupFile? ChannelMap { get; set; }
    public ConfigBackupFile? Settings { get; set; }
}

public class ConfigBackupFile
{
    public string Path { get; set; } = "";
    public string Content { get; set; } = "";
}

internal sealed record PendingFileWrite(string Path, string Content);
internal sealed record FileSnapshot(string Path, bool Existed, string? OriginalContent);
