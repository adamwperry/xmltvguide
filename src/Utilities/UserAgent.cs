namespace xmlTVGuide.Utilities;

/// <summary>
/// This record struct represents a User-Agent string.
/// It is used to identify the client software making a request to a web server.
/// </summary>
/// <param name="Value">The User-Agent string value.</param>
public readonly record struct UserAgent(string Value)
{
    public static UserAgent Chrome => new("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                                         "(KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

    public static UserAgent Firefox => new("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:89.0) " +
                                          "Gecko/20100101 Firefox/89.0");

    public static UserAgent Edge => new("Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                                        "AppleWebKit/537.36 (KHTML, like Gecko) " +
                                        "Chrome/91.0.4472.124 Safari/537.36 Edg/91.0.864.59");
}
