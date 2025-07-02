using System.Collections.Generic;

namespace xmlTVGuide.Models;

public class ParsedArguments
{
    public bool Fake { get; set; }
    public List<string> Urls { get; set; } = new List<string>();
    public string? ChannelMapPath { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public bool HelpSet { get; set; }
}
