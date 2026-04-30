using xmlTVGuide.Models;

namespace xmlTVGuide.Services.ChannelMap;

public class ChannelMapAnalysis
{
    public int TotalEntries { get; set; }
    public int ValidEntries { get; set; }
    public int BlankChannelIdCount { get; set; }
    public int BlankNameCount { get; set; }
    public int DuplicateChannelIdCount { get; set; }
    public List<ChannelMapDto> ValidChannels { get; set; } = new();
    public List<ChannelMapEntryIssue> BlankChannelIdEntries { get; set; } = new();
    public List<ChannelMapEntryIssue> BlankNameEntries { get; set; } = new();
    public List<DuplicateChannelMapGroup> DuplicateChannelIdGroups { get; set; } = new();
    public bool HasWarnings =>
        BlankChannelIdCount > 0 ||
        BlankNameCount > 0 ||
        DuplicateChannelIdCount > 0;
}

public class ChannelMapEntryIssue
{
    public int EntryNumber { get; set; }
    public string? Name { get; set; }
    public string? ChannelId { get; set; }
}

public class DuplicateChannelMapGroup
{
    public string ChannelId { get; set; } = "";
    public List<int> EntryNumbers { get; set; } = new();
    public List<string> Names { get; set; } = new();
}
