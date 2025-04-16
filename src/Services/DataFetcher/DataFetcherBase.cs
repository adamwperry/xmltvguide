using System;
using System.Net.Http;
using System.Threading.Tasks;
using xmlTVGuide.Utilities;

namespace xmlTVGuide.Services;

/// <summary>
/// This abstract class serves as a base for data fetching services.
/// It provides a method to fetch data from a given URL and a method to set the Unix time in the URL.
/// The class also initializes an HttpClient with default headers.
/// </summary>
public abstract class DataFetcherBase : IDataFetcher
{
    protected const string UnixTimePlaceholder = "{unixtime}";

    protected HttpClient _client;

    public DataFetcherBase()
    {
        _client = GetClientAsync(UserAgent.Chrome);
    }
    /// <summary>
    /// Fetches data from the specified URL using an HttpClient.
    /// </summary>
    /// <param name="url">The URL to fetch data from.</param>
    /// <returns>Returns the content of the response as a string.</returns>
    public abstract Task<string> FetchDataAsync(string url);

    /// <summary>
    /// Initializes an HttpClient with default headers.
    /// The User-Agent header is set to the specified user agent.
    /// The Accept, Accept-Language, and Connection headers are also set to common values.
    /// </summary>
    /// <param name="userAgent">The user agent to be used in the request.</param>
    /// <returns>Returns an instance of HttpClient with the specified headers.</returns>
    protected HttpClient GetClientAsync(UserAgent userAgent)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", userAgent.Value);

        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        client.DefaultRequestHeaders.Add("Connection", "keep-alive");

        return client;
    }

    /// <summary>
    /// Sets the Unix time in the URL.
    /// This method replaces the placeholder "{unixtime}" in the URL with the current Unix time.
    /// </summary>
    /// <param name="url">The URL to be modified.</param>
    /// <returns>Returns the modified URL with the Unix time.</returns>
    protected string SetUnixTime(string url)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));

        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return url.Replace(UnixTimePlaceholder, unixTime.ToString());
    }
}