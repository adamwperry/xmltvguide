using System.Text.Json.Nodes;

namespace xmlTVGuide.Services;

/// <summary>
/// This interface defines the contract for building an XML TV file from JSON EPG data.
/// It includes a method to build the XML TV file using the provided EPG data,
/// channel map path, and output path.
/// </summary>
public interface IXmlTVBuilder
{
    void BuildXmlTV(JsonObject epgData, string channelMapPath, string outputPath);
}
