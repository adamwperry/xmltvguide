using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace xmlTVGuide.Services;

/// <summary>
/// This class is responsible for fetching data from a given URL.
/// It inherits from the DataFetcherBase class and implements the FetchDataAsync method.
/// The class uses an HttpClient to make HTTP GET requests.
/// </summary>
public class DataFetcher : DataFetcherBase
{
    /// <summary>
    /// Fetches data from the specified URL using an HttpClient.
    /// If the URL contains the placeholder "{unixtime}", it replaces it with the current Unix time.
    /// </summary>
    /// <param name="url">The URL to fetch data from.</param>
    /// <returns>Returns the content of the response as a string.</returns>
    public override async Task<string> FetchDataAsync(string url)
    {
        if (_client == null)
            throw new InvalidOperationException("HttpClient is not initialized.");

        if (string.IsNullOrEmpty(url))
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));

        if (url.Contains(UnixTimePlaceholder))
            url = SetUnixTime(url);

        try
        {
            using var response = await _client.GetAsync(url);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();

            throw new HttpRequestException($"Failed to fetch data from {url}. Status code: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching data from {url}: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Fetches data from a list of URLs asynchronously.
    /// </summary>
    /// <param name="urls"><see cref="List{string}"/> of URLs to fetch data from.</param>
    /// <returns><see cref="Task{List{string}}"/> containing the fetched data from each URL.</returns>
    public override async Task<List<string>> FetchDataAsync(List<string> urls)
    {
        if (urls == null || urls.Count == 0)
            throw new ArgumentException("URL list cannot be null or empty.", nameof(urls));

        var tasks = urls.ConvertAll(url => FetchDataAsync(url));
        var results = await Task.WhenAll(tasks);
        return new List<string>(results);
    }
}
