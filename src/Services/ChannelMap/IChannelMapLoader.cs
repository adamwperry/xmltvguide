using System.Collections.Generic;
using xmlTVGuide.Models;

namespace xmlTVGuide.Services.ChannelMap;

/// <summary>
/// This interface defines the contract for loading channel maps.
/// </summary>
public interface IChannelMapLoader
{
    List<ChannelMapDto> LoadChannelMap(string filePath);
}