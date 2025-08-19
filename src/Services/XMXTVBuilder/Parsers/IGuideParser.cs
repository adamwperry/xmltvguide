using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using xmlTVGuide.Models;

namespace xmlTVGuide.Services.XMXTVBuilder.Parsers;

/// <summary>
/// This interface defines the contract for parsing EPG data into XML TV format.
/// It includes methods to process channels and check if the parser can handle the provided EPG data
/// </summary>
public interface IGuideParser
{
    /// <summary>
    /// Processes the channels from the EPG data and builds the XML structure.
    /// This method extracts channel information from the provided EPG data and constructs
    /// the XML structure required for the xmlTV format.
    /// </summary>
    /// <param name="tv"><see cref="XElement"/> representing the XML TV structure.</param>
    /// <param name="epg"><see cref="JsonObject"/> representing the EPG data.</param>
    /// <param name="channelMap"><see cref="List{ChannelMapDto}"/> representing the channel mapping.</param>
    /// <returns>
    /// A <see cref="XElement"/> representing the updated XML TV structure with channels processed.
    /// </returns>
    public XElement ProcessChannels(XElement tv, JsonObject epg, List<ChannelMapDto>? channelMap);

    /// <summary>
    /// Checks if the parser can handle the provided EPG data.
    /// This method checks the basic structure of the EPG data to determine if it is compatible
    /// with the parser's expected format. It returns true if the parser can handle the data,
    /// otherwise false.
    /// </summary>
    /// <param name="epg"><see cref="JsonObject"/> representing the EPG data.</param>
    /// <returns>
    /// A <see cref="bool"/> indicating whether the parser can handle the provided EPG data.
    /// </returns>
    public bool CanParse(JsonObject epg);
}