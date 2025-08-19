using System.Collections.Generic;

namespace xmlTVGuide.Services;

/// <summary>
/// This interface defines the contract for building an XML TV file from JSON EPG data.
/// It includes a method to build the XML TV file using the provided EPG data,
/// channel map path, and output path.
/// </summary>
public interface IXmlTVBuilder
{
    /// <summary>
    /// Builds an XML TV file from the provided EPG data.
    /// </summary>
    /// <param name="epgData"><see cref="List{string}"/> containing EPG data in JSON format.</param>
    /// <param name="channelMapPath"><see cref="string"/> path to the channel map file.</param>
    /// <param name="outputPath"><see cref="string"/> path where the XML TV file will be saved.</param>
    void BuildXmlTV(List<string> epgData, string channelMapPath, string outputPath);
}
