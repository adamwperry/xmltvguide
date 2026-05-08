namespace xmlTVGuide.Services.AppSettings;

public class AppSettings
{
    public ChannelOutputSettings Channel { get; set; } = new();
}

public class ChannelOutputSettings
{
    public bool UseChannelNamesInsteadOfNumericIds { get; set; }
    public bool SortChannelsByIdThenDisplayName { get; set; } = true;
}
