using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using xmlTVGuide.Utilities;

namespace xmlTVGuide.Services;

/// <summary>
/// This interface defines the contract for data fetching services.
/// It includes a method to fetch data from a given URL.
/// </summary>
public interface IDataFetcher
{
    Task<string> FetchDataAsync(string url);
}



