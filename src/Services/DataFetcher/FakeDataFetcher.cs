using System.Diagnostics;

namespace xmlTVGuide.Services;

/// <summary>
/// This class is a mock implementation of the IDataFetcher interface.
/// It simulates fetching data from a file instead of making an actual HTTP request.
/// This is useful for testing purposes.
/// The FetchDataAsync method reads the content of a file specified by the URL parameter.
/// </summary>
public class FakeDataFetcher : DataFetcherBase
{
    public override Task<string> FetchDataAsync(string url, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(url))
            throw new FileNotFoundException("The file 'url' does not exist.");

        string content = File.ReadAllText(url);
        return Task.FromResult(content);
    }

    public override Task<List<string>> FetchDataAsync(List<string> urls, CancellationToken cancellationToken = default)
    {
        //@todo: Implement this method to fetch data from multiple files.
        throw new NotImplementedException("This method is not implemented in the FakeDataFetcher class.");
    }

    /// <summary>
    /// Fetches data from a single file and returns detailed result with error information.
    /// </summary>
    public override Task<FetchResult> FetchDataWithResultAsync(string url, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = new FetchResult
        {
            Url = url,
            FetchedAt = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (!File.Exists(url))
            {
                stopwatch.Stop();
                result.Success = false;
                result.ErrorMessage = $"File not found: {url}";
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                return Task.FromResult(result);
            }

            result.Data = File.ReadAllText(url);
            cancellationToken.ThrowIfCancellationRequested();
            result.ResponseSize = result.Data.Length;
            result.Success = true;
            stopwatch.Stop();
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = $"Error reading file: {ex.Message}";
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Fetches data from multiple files and returns detailed results with error information for each.
    /// </summary>
    public override async Task<List<FetchResult>> FetchDataWithResultsAsync(List<string> urls, CancellationToken cancellationToken = default)
    {
        if (urls == null || urls.Count == 0)
            throw new ArgumentException("URL list cannot be null or empty.", nameof(urls));

        var results = new List<FetchResult>();
        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await FetchDataWithResultAsync(url, cancellationToken);
            results.Add(result);
        }

        return results;
    }
}
