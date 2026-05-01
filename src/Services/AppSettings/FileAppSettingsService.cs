using System.Text.Json;
using IOFile = System.IO.File;

namespace xmlTVGuide.Services.AppSettings;

public class FileAppSettingsService : IAppSettingsService
{
    private const string UseChannelNamesEnv = "USE_CHANNEL_NAMES_INSTEAD_OF_NUMERIC_IDS";
    private const string StripChannelNumbersEnv = "STRIP_CHANNEL_NUMBERS";
    private const string SortChannelsEnv = "SORT_CHANNELS_BY_ID";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public FileAppSettingsService()
    {
        var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
                       Directory.Exists("/app");
        var basePath = isDocker ? "/app" : Directory.GetCurrentDirectory();

        SettingsPath = Path.GetFullPath(
            Environment.GetEnvironmentVariable("SETTINGS_PATH") ??
            Path.Combine(basePath, "settings.json")
        );
    }

    public string SettingsPath { get; }

    public async Task<AppSettings> LoadAsync()
    {
        AppSettings settings;

        if (!IOFile.Exists(SettingsPath))
        {
            settings = new AppSettings();
        }
        else
        {
            var content = await IOFile.ReadAllTextAsync(SettingsPath);
            settings = string.IsNullOrWhiteSpace(content)
                ? new AppSettings()
                : JsonSerializer.Deserialize<AppSettings>(content, JsonOptions) ?? new AppSettings();
        }

        ApplyEnvironmentOverrides(settings);
        return settings;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        settings.Channel ??= new ChannelOutputSettings();

        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath) ?? Directory.GetCurrentDirectory());
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await IOFile.WriteAllTextAsync(SettingsPath, json + "\n");
    }

    private static void ApplyEnvironmentOverrides(AppSettings settings)
    {
        settings.Channel ??= new ChannelOutputSettings();

        var useChannelNames = GetEnvironmentBoolean(UseChannelNamesEnv, StripChannelNumbersEnv);
        if (useChannelNames.HasValue)
            settings.Channel.UseChannelNamesInsteadOfNumericIds = useChannelNames.Value;

        var sortChannels = GetEnvironmentBoolean(SortChannelsEnv);
        if (sortChannels.HasValue)
            settings.Channel.SortChannelsByIdThenDisplayName = sortChannels.Value;
    }

    private static bool? GetEnvironmentBoolean(params string[] names)
    {
        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return null;
    }
}
