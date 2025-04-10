namespace xmlTVGuide.Models;

public class ParsedArguments
{
    public bool Fake { get; set; }
    public string Url { get; set; }
    public string ChannelMapPath { get; set; }
    public string OutputPath { get; set; }
    public bool HelpSet { get; set; }
}