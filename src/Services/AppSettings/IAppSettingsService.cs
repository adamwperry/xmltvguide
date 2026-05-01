namespace xmlTVGuide.Services.AppSettings;

public interface IAppSettingsService
{
    string SettingsPath { get; }
    Task<AppSettings> LoadAsync();
    Task SaveAsync(AppSettings settings);
}
