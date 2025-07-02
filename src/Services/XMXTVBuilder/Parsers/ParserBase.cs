using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using xmlTVGuide.Models;

namespace xmlTVGuide.Services.XMXTVBuilder.Parsers;

/// <summary>
/// Base class for XML TV parsers.
/// This class defines the common structure and methods for parsing XML TV data.
/// It includes constants for XML element names and a method to parse Unix time.
/// Derived classes should implement the CanParse and ProcessChannels methods.
/// </summary>
public abstract class ParserBase : IGuideParser
{
    // XML TV element names
    protected const string CallSignKey = "callSign";
    protected const string ChannelIdKey = "channelId";
    protected const string ChannelKey = "channel";
    protected const string ChannelNoKey = "channelNo";
    protected const string ChannelsKey = "channels";
    protected const string DataKey = "data";
    protected const string DescKey = "desc";
    protected const string DisplayNameKey = "display-name";
    protected const string EnKey = "en";
    protected const string EndTimeKey = "endTime";
    protected const string EventsKey = "events";
    protected const string IconKey = "icon";
    protected const string IdKey = "id";
    protected const string ItemsKey = "items";
    protected const string LangKey = "lang";
    protected const string LogoKey = "logo";
    protected const string NameKey = "name";
    protected const string NetworkNameKey = "networkName";
    protected const string ProgramKey = "program";
    protected const string ProgrammeKey = "programme";
    protected const string ProgramSchedulesKey = "programSchedules";
    protected const string ShortDescKey = "shortDesc";
    protected const string SourceIdKey = "sourceId";
    protected const string SrcKey = "src";
    protected const string StartKey = "start";
    protected const string StartTimeKey = "startTime";
    protected const string StopKey = "stop";
    protected const string ThumbnailKey = "thumbnail";
    protected const string TitleKey = "title";
    protected const string TVKey = "tv";


    /// <summary>
    /// Checks if the parser can handle the provided EPG data.
    /// This method should be implemented by derived classes to determine if they can parse the given JSON object.
    /// The method should return true if the parser can handle the EPG data, otherwise false.
    /// </summary>
    /// <param name="epg">JSON object <see cref="JsonObject"/> representing the EPG data.</param>
    /// <returns>
    /// A boolean value indicating whether the parser can handle the provided EPG data.
    /// </returns>
    public abstract bool CanParse(JsonObject epg);

    /// <summary>
    /// Processes the channels from the provided EPG data and returns an XML representation.
    /// This method should be implemented by derived classes to parse the channels and their programs from the EPG data.
    /// It should return an <see cref="XElement"/> representing the channels in XML format.
    /// The method may also use a channel map to map channel IDs to display names or other attributes.
    /// </summary>
    /// <param name="tv">
    /// The XML element <see cref="XElement"/> representing the TV structure to which channels will be added.
    /// </param>
    /// <param name="epg">The EPG data as a JSON object <see cref="JsonObject"/>.</param>
    /// <param name="channelMap">
    /// An optional list of channel mappings <see cref="List{ChannelMapDto}"/>
    /// that can be used to map channel IDs to display names or other attributes.
    /// </param>
    /// <returns>
    /// An <see cref="XElement"/> representing the processed channels in XML format.
    /// This element will be added to the provided `tv` element.
    /// </returns>
    public abstract XElement ProcessChannels(XElement tv, JsonObject epg, List<ChannelMapDto>? channelMap);

    /// <summary>
    /// Parses a Unix time string and converts it to a formatted date string.
    /// This method takes a Unix time string, converts it to a DateTime object,
    /// and formats it as a string in the "yyyyMMddHHmmss +0000" format.
    /// If the input string is not a valid Unix time, it returns null.
    /// </summary>
    /// <param name="unixTimeStr">The Unix time string to parse.</param>
    /// <returns>
    /// A formatted date string in the "yyyyMMddHHmmss +0000" format
    /// if the input string is a valid Unix time;
    /// otherwise, returns null.
    /// </returns>
    protected static string? ParseUnixTime(string? unixTimeStr)
    {
        if (long.TryParse(unixTimeStr, out var unixTime))
        {
            var dt = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
            return dt.ToString("yyyyMMddHHmmss") + " +0000";
        }
        return null;
    }
}