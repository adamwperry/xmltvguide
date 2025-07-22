using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using xmlTVGuide.Models;

namespace xmlTVGuide.Services.XMXTVBuilder.Parsers;

public class GuideThreeParser : ParserBase, IGuideParser
{
    private const string StreamsKey = "streams";
    private const string ContentKey = "content";
    private const string StartDateKey = "start_date";
    private const string EndDateKey = "end_date";

    public override bool CanParse(JsonObject epgData)
    {
        if (!epgData.TryGetPropertyValue(ItemsKey, out var itemsNode) || itemsNode is not JsonArray items || items.Count == 0)
            return false;

        var firstItem = items[0] as JsonObject;
        if (firstItem == null || !firstItem.TryGetPropertyValue(ContentKey, out var contentNode) || contentNode is not JsonObject contentObj)
            return false;

        var streams = contentObj[StreamsKey] as JsonArray;
        if (streams is null || streams.Count == 0)
            return false;

        var sample = streams[0] as JsonObject;
        return sample != null
            && sample.ContainsKey(ChannelKey)
            && sample.ContainsKey(TitleKey)
            && sample.ContainsKey(StartDateKey)
            && sample.ContainsKey(EndDateKey);
    }

    public override XElement ProcessChannels(XElement tv, JsonObject epg, List<ChannelMapDto>? channelMap)
    {
        if (!epg.TryGetPropertyValue(ItemsKey, out var itemsNode) || itemsNode is not JsonArray items)
            return tv;

        foreach (var item in items.OfType<JsonObject>())
        {
            if (!item.TryGetPropertyValue(ContentKey, out var contentNode) || contentNode is not JsonObject content)
                continue;

            if (!content.TryGetPropertyValue(StreamsKey, out var streamsNode) || streamsNode is not JsonArray streams)
                continue;

            foreach (var stream in streams.OfType<JsonObject>())
            {
                var channelId = stream[ChannelKey]?.ToString();
                var title = stream[TitleKey]?.ToString();
                var desc = stream[DescKey]?.ToString();
                var thumb = stream[ThumbnailKey]?.ToString();
                var start = ParseDateTime(stream[StartDateKey]?.ToString());
                var stop = ParseDateTime(stream[EndDateKey]?.ToString());

                if (string.IsNullOrWhiteSpace(channelId) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(stop))
                    continue;

                if (!tv.Elements(ChannelKey).Any(e => e.Attribute(IdKey)?.Value == channelId))
                {
                    var mapped = channelMap?.FirstOrDefault(m => m.ChannelId == channelId);
                    var displayName = mapped != null ? mapped.Name : channelId;

                    var chan = new XElement(ChannelKey, new XAttribute(IdKey, channelId));
                    chan.Add(new XElement(DisplayNameKey, displayName));
                    if (!string.IsNullOrWhiteSpace(thumb))
                        chan.Add(new XElement(IconKey, new XAttribute(SrcKey, thumb)));

                    tv.Add(chan);
                }

                var programme = new XElement(ProgrammeKey,
                    new XAttribute(StartKey, start),
                    new XAttribute(StopKey, stop),
                    new XAttribute(ChannelKey, channelId));

                programme.Add(new XElement(TitleKey, new XAttribute(LangKey, EnKey), title));
                if (!string.IsNullOrWhiteSpace(desc))
                    programme.Add(new XElement(DescKey, new XAttribute(LangKey, EnKey), desc));

                tv.Add(programme);
            }
        }

        return tv;
    }

    private static string ParseDateTime(string? dt)
    {
        if (DateTime.TryParse(dt, out var result))
            return result.ToUniversalTime().ToString("yyyyMMddHHmmss") + " +0000";

        return "";
    }
}
