using System.IO;
using System.Threading.Tasks;

namespace xmlTVGuide.Services;


/// <summary>
/// This class is a mock implementation of the IDataFetcher interface.
/// It simulates fetching data from a file instead of making an actual HTTP request.
/// This is useful for testing purposes.
/// The FetchDataAsync method reads the content of a file specified by the URL parameter.
/// </summary>
public class FakeDataFetcher : DataFetcherBase
{
    public override Task<string> FetchDataAsync(string url)
    {
        if (!File.Exists(url))
            throw new FileNotFoundException("The file 'url' does not exist.");

        string content = File.ReadAllText(url);
        return Task.FromResult(content);
    }
}