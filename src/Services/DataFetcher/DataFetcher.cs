using System.Diagnostics;

namespace xmlTVGuide.Services;

/// <summary>
/// This class is responsible for fetching data from given URLs.
/// It inherits from the DataFetcherBase class and implements the FetchDataAsync method.
/// The class uses an HttpClient to make HTTP GET requests.
/// </summary>
public class DataFetcher : DataFetcherBase
{
    /// <summary>
    /// Fetches data from the specified URL using an HttpClient.
    /// If the URL contains the placeholder "{unixtime}", it replaces it with the current Unix time.
    /// Returns empty string on failure (legacy behavior - use FetchDataWithResultAsync for error details).
    /// </summary>
    /// <param name="url">The URL to fetch data from.</param>
    /// <returns>Returns the content of the response as a string, or empty string on failure.</returns>
    public override async Task<string> FetchDataAsync(string url)
    {
        if (_client == null)
            throw new InvalidOperationException("HttpClient is not initialized.");

        if (string.IsNullOrEmpty(url))
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));

        if (url.Contains(UnixTimePlaceholder))
            url = SetUnixTime(url);

        if (url.Contains(YearMonthPlaceholder))
            url = SetMonthYearTime(url);

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
    /// Returns empty strings for failed URLs (legacy behavior - use FetchDataWithResultsAsync for error details).
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

    /// <summary>
    /// Fetches data from a single URL and returns detailed result including error information.
    /// This method provides per-source error diagnostics.
    /// </summary>
    /// <param name="url">The URL to fetch data from.</param>
    /// <returns>A FetchResult containing data, success status, and detailed error info if fetch failed.</returns>
    public override async Task<FetchResult> FetchDataWithResultAsync(string url)
    {
        if (_client == null)
            throw new InvalidOperationException("HttpClient is not initialized.");

        if (string.IsNullOrEmpty(url))
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));

        var result = new FetchResult
        {
            Url = url,
            FetchedAt = DateTime.UtcNow
        };

        if (url.Contains(UnixTimePlaceholder))
            url = SetUnixTime(url);

        if (url.Contains(YearMonthPlaceholder))
            url = SetMonthYearTime(url);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _client.GetAsync(url);
            stopwatch.Stop();

            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            result.StatusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                result.Data = await response.Content.ReadAsStringAsync();
                result.ResponseSize = result.Data.Length;
                result.Success = true;
                return result;
            }

            result.Success = false;
            result.ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.StatusCode}";
            result.ResponseSize = response.Content.Headers.ContentLength ?? 0;
            return result;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            result.Success = false;
            result.ErrorMessage = $"HTTP Request Error: {ex.Message}";
            return result;
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            result.Success = false;
            result.ErrorMessage = $"Request Timeout: {ex.Message}";
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            result.Success = false;
            result.ErrorMessage = $"Error: {ex.GetType().Name} - {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Fetches data from multiple URLs and returns detailed results for each, including error information.
    /// This method provides per-source error diagnostics for all URLs.
    /// </summary>
    /// <param name="urls">List of URLs to fetch data from.</param>
    /// <returns>A list of FetchResult objects, one per URL, containing data and detailed error info.</returns>
    public override async Task<List<FetchResult>> FetchDataWithResultsAsync(List<string> urls)
    {
        if (urls == null || urls.Count == 0)
            throw new ArgumentException("URL list cannot be null or empty.", nameof(urls));

        var tasks = urls.ConvertAll(url => FetchDataWithResultAsync(url));
        var results = await Task.WhenAll(tasks);
        return new List<FetchResult>(results);
    }
}

