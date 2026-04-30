namespace xmlTVGuide.Services;

/// <summary>
/// This interface defines the contract for data fetching services.
/// It includes methods to fetch data from given URLs with detailed error reporting.
/// </summary>
public interface IDataFetcher
{
    /// <summary>
    /// Fetches data from a single URL and returns the raw string content.
    /// Legacy method - use FetchDataWithResultAsync for detailed error information.
    /// </summary>
    Task<string> FetchDataAsync(string url);

    /// <summary>
    /// Fetches data from multiple URLs and returns a list of raw string content.
    /// Legacy method - use FetchDataWithResultsAsync for detailed error information per source.
    /// </summary>
    Task<List<string>> FetchDataAsync(List<string> urls);

    /// <summary>
    /// Fetches data from a single URL and returns detailed result including error information.
    /// </summary>
    /// <param name="url">The URL to fetch data from.</param>
    /// <returns>A FetchResult containing data and detailed error info if fetch failed.</returns>
    Task<FetchResult> FetchDataWithResultAsync(string url);

    /// <summary>
    /// Fetches data from multiple URLs and returns detailed results for each including error information.
    /// </summary>
    /// <param name="urls">List of URLs to fetch data from.</param>
    /// <returns>A list of FetchResult objects, one per URL, containing data and detailed error info.</returns>
    Task<List<FetchResult>> FetchDataWithResultsAsync(List<string> urls);

    Task<bool> ValidateUrl(string url);
}
