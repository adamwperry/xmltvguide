namespace xmlTVGuide.Services;

/// <summary>
/// Represents the result of a fetch operation for a single URL.
/// Contains both the fetched data and error information if the fetch failed.
/// </summary>
public class FetchResult
{
    /// <summary>
    /// Gets or sets the URL that was fetched.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the fetched data content. Empty string if fetch failed.
    /// </summary>
    public string? Data { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the fetch was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if the fetch failed. Null or empty if successful.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code if applicable. Null if successful or connection failed.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the response size in bytes.
    /// </summary>
    public long ResponseSize { get; set; }

    /// <summary>
    /// Gets or sets the response time in milliseconds.
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the fetch was attempted.
    /// </summary>
    public DateTime FetchedAt { get; set; }
}
