using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using xmlTVGuide.Models;
using System.Threading.Tasks;

namespace xmlTVGuide.Services.XMXTVBuilder.Parsers;

/// <summary>
/// Parser for GuideTwo EPG data.
/// This parser processes EPG data from GuideTwo, extracting channel and program information.
/// It builds the XML structure required for the xmlTV format.
/// </summary>
public class GuideTwoParser
    : ParserBase
    , IGuideParser
{
    private readonly IDataFetcher _dataFetcher;

    public GuideTwoParser(IDataFetcher dataFetcher)
    {
        _dataFetcher = dataFetcher;
    }
    // Uses this git repository for TV logos:
    private const string TvLogosBaseUrl =
        "https://raw.githubusercontent.com/tv-logo/tv-logos/refs/heads/main/countries/united-states/";

    /// <summary>
    /// Checks if the parser can handle the provided EPG data.
    /// </summary>
    /// <param name="epg"><see cref="JsonObject"/> representing the EPG data.</param>
    /// <returns>
    /// A <see cref="bool"/> indicating whether the parser can handle the provided EPG data.
    /// </returns>
    public override bool CanParse(JsonObject epg)
    {
        if (!epg.TryGetPropertyValue(DataKey, out var dataNode))
            return false;

        if (dataNode is not JsonObject dataObj)
            return false;

        return dataObj.TryGetPropertyValue(ItemsKey, out var itemsNode) &&
               itemsNode is JsonArray items &&
               items.Count > 0 &&
               items[0]?[ChannelKey]?[SourceIdKey] != null &&
               items[0]?[ChannelKey]?[NetworkNameKey] != null;
    }

    /// <summary>
    /// Processes the channels and programs from the EPG data and builds the XML structure.
    /// </summary>
    /// <param name="tv"><see cref="XElement"/> representing the XML TV structure.</param>
    /// <param name="epg"><see cref="JsonObject"/> containing the EPG data.</param>
    /// <param name="channelMap">Optional list of <see cref="ChannelMapDto"/> for channel name mapping.</param>
    /// <returns>
    /// An <see cref="XElement"/> representing the updated XML TV structure with channels and programs added.
    /// </returns>
    public override XElement ProcessChannels(XElement tv, JsonObject epg, List<ChannelMapDto>? channelMap)
    {
        if (!epg.TryGetPropertyValue(DataKey, out var dataNode) || dataNode is not JsonObject dataObj)
            return tv;

        if (!dataObj.TryGetPropertyValue(ItemsKey, out var itemsNode) || itemsNode is not JsonArray items)
            return tv;

        foreach (var item in DistinctAndSortedByChannel(items))
        // foreach (var item in items)
        {
            var channelNode = item?[ChannelKey];
            var scheduleArray = item?[ProgramSchedulesKey] as JsonArray;
            if (channelNode is null || scheduleArray is null)
                continue;

            var sourceId = channelNode[SourceIdKey]?.ToString();
            if (string.IsNullOrWhiteSpace(sourceId))
                continue;

            var channelId = sourceId;
            var displayName = GetMappedChannelName(channelMap, sourceId) ?? channelNode[NetworkNameKey]?.ToString();
            var logo = channelNode[LogoKey]?.ToString();

            if (string.IsNullOrWhiteSpace(channelId) || string.IsNullOrWhiteSpace(displayName))
                continue;

            var channelElement = new XElement(ChannelKey, new XAttribute(IdKey, channelId));
            channelElement.Add(new XElement(DisplayNameKey, displayName));

            if (!string.IsNullOrWhiteSpace(logo))
                channelElement.Add(BuildChannelIconElementAsync(channelNode).GetAwaiter().GetResult());

            tv.Add(channelElement);

            foreach (var prog in scheduleArray)
            {
                var start = ParseUnixTime(prog?[StartTimeKey]?.ToString());
                var end = ParseUnixTime(prog?[EndTimeKey]?.ToString());
                var title = prog?[TitleKey]?.ToString();

                if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end) || string.IsNullOrWhiteSpace(title))
                    continue;

                var progElement = new XElement(ProgrammeKey,
                    new XAttribute(StartKey, start),
                    new XAttribute(StopKey, end),
                    new XAttribute(ChannelKey, channelId));

                progElement.Add(new XElement(TitleKey, new XAttribute(LangKey, EnKey), title));
                tv.Add(progElement);
            }
        }
        return tv;
    }

    /// <summary>
    /// Distincts and sorts the items by channel source ID and network name.
    /// This method filters the items to ensure each channel is represented only once,
    /// and sorts them by the network name.
    /// </summary>
    /// <param name="items"><see cref="JsonArray"/> containing the items to be processed.</param>
    /// <returns>
    /// A <see cref="JsonArray"/> containing distinct and sorted items by channel source ID and network name.
    /// </returns>
    private JsonArray DistinctAndSortedByChannel(JsonArray items) =>
        new JsonArray(
            items
                .Where(item => item?[ChannelKey]?[SourceIdKey] != null)
                .GroupBy(item => item?[ChannelKey]?[SourceIdKey]?.ToString())
                .Select(g => g.First())
                .OrderBy(item => item?[ChannelKey]?[NetworkNameKey]?.ToString())
                .Select(item => item is null ? null : JsonNode.Parse(item.ToJsonString()))
                .ToArray()
        );

    /// <summary>
    /// Looks up the mapped channel name from the provided channel map.
    /// </summary>
    /// <param name="channelMap">List of <see cref="ChannelMapDto"/> containing channel mappings.</param>
    /// <param name="sourceId">The source ID of the channel to look up.</param>
    /// <returns>
    /// The mapped channel name if found; otherwise, null.
    /// </returns>
    private static string? GetMappedChannelName(List<ChannelMapDto>? channelMap, string sourceId)
    {
        return channelMap?.FirstOrDefault(c => c.ChannelId == sourceId)?.Name;
    }

    /// <summary>
    /// Builds the channel icon element for the XML TV structure.
    /// </summary>
    /// <param name="channelNode"><see cref="JsonNode"/> representing the channel data.</param>
    /// <returns>
    /// An <see cref="XElement"/> representing the channel icon, or null if the icon cannot be built.
    /// </returns>
    private async Task<XElement?> BuildChannelIconElementAsync(JsonNode? channelNode)
    {
        if (channelNode is not JsonObject channel) return null;

        var networkName = channel[NameKey]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(networkName)) return null;

        var fullUrl = await BuildChannelIconUrlAsync(networkName);
        return string.IsNullOrWhiteSpace(fullUrl)
            ? null
            : new XElement(IconKey, new XAttribute(SrcKey, fullUrl));
    }

    /// <summary>
    /// Builds the URL for the channel icon based on the network name.
    /// </summary>
    /// <param name="networkName"><see cref="string"/> representing the network name.</param>
    /// <returns>
    /// A <see cref="string"/> representing the URL of the channel icon, or an empty string if the URL is invalid.
    /// </returns>
    private async Task<string> BuildChannelIconUrlAsync(string networkName)
    {
        if (string.IsNullOrWhiteSpace(networkName))
            return string.Empty;

        var safeName = networkName.ToLowerInvariant().Replace(" ", "-"); // basic sanitization
        var url = $"{TvLogosBaseUrl}{safeName}-us.png";
        return await _dataFetcher.ValidateUrl(url) 
            ? url
            : string.Empty;
    }
}
