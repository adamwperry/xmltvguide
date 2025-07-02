using System.Collections.Generic;
using System.Threading.Tasks;

namespace xmlTVGuide.Services;

/// <summary>
/// This interface defines the contract for data fetching services.
/// It includes a method to fetch data from a given URL.
/// </summary>
public interface IDataFetcher
{
    Task<string> FetchDataAsync(string url);
    Task<List<string>> FetchDataAsync(List<string> urls);
}
