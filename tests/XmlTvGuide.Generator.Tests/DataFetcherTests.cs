using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using xmlTVGuide.Services;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class DataFetcherTests
{
    [Fact]
    public async Task fetch_data_async_returns_content_on_success()
    {
        var fetcher = CreateFetcher(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("payload")
        }));

        var result = await fetcher.FetchDataAsync("https://example.com/feed");

        result.Should().Be("payload");
    }

    [Fact]
    public async Task fetch_data_async_returns_empty_string_on_http_failure()
    {
        var fetcher = CreateFetcher(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var result = await fetcher.FetchDataAsync("https://example.com/feed");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task fetch_data_async_replaces_placeholders_before_request()
    {
        HttpRequestMessage? capturedRequest = null;
        var fetcher = CreateFetcher(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("payload")
            };
        }));

        await fetcher.FetchDataAsync("https://example.com/feed?ts={unixtime}&ym={monthyear}");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().NotContain("{unixtime}");
        capturedRequest.RequestUri!.ToString().Should().NotContain("{monthyear}");
    }

    [Fact]
    public async Task fetch_data_async_throws_for_empty_url()
    {
        var fetcher = CreateFetcher(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var act = async () => await fetcher.FetchDataAsync("");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("URL cannot be null or empty.*");
    }

    [Fact]
    public async Task fetch_data_async_list_returns_results_for_each_url()
    {
        var fetcher = CreateFetcher(new StubHttpMessageHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.RequestUri!.ToString())
        }));

        var result = await fetcher.FetchDataAsync(new List<string>
        {
            "https://example.com/one",
            "https://example.com/two"
        });

        result.Should().HaveCount(2);
        result[0].Should().Contain("/one");
        result[1].Should().Contain("/two");
    }

    [Fact]
    public async Task fetch_data_with_result_async_returns_success_metadata()
    {
        var fetcher = CreateFetcher(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("payload", Encoding.UTF8)
        }));

        var result = await fetcher.FetchDataWithResultAsync("https://example.com/feed");

        result.Success.Should().BeTrue();
        result.Data.Should().Be("payload");
        result.ResponseSize.Should().Be(7);
        result.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task fetch_data_with_result_async_returns_failure_metadata_for_http_error()
    {
        var fetcher = CreateFetcher(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("bad")
        }));

        var result = await fetcher.FetchDataWithResultAsync("https://example.com/feed");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP 502");
        result.StatusCode.Should().Be(502);
    }

    [Fact]
    public async Task fetch_data_with_result_async_returns_failure_metadata_for_http_request_exception()
    {
        var fetcher = CreateFetcher(new StubHttpMessageHandler(_ => throw new HttpRequestException("network down")));

        var result = await fetcher.FetchDataWithResultAsync("https://example.com/feed");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP Request Error");
    }

    [Fact]
    public async Task fetch_data_with_results_async_returns_entry_per_url()
    {
        var fetcher = CreateFetcher(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ok")
        }));

        var result = await fetcher.FetchDataWithResultsAsync(new List<string>
        {
            "https://example.com/one",
            "https://example.com/two"
        });

        result.Should().HaveCount(2);
        result.Should().OnlyContain(entry => entry.Success);
    }

    [Fact]
    public async Task validate_url_returns_true_on_success_status_code()
    {
        var fetcher = CreateFetcher(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var result = await fetcher.ValidateUrl("https://example.com/feed");

        result.Should().BeTrue();
    }

    private static TestableDataFetcher CreateFetcher(HttpMessageHandler handler)
    {
        var fetcher = new TestableDataFetcher();
        fetcher.SetClient(new HttpClient(handler));
        return fetcher;
    }

    private sealed class TestableDataFetcher : DataFetcher
    {
        public void SetClient(HttpClient client)
        {
            _client = client;
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
