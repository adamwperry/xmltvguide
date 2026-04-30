using FluentAssertions;
using xmlTVGuide.Services.ArgumentParser;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class ArgumentParserTests : IDisposable
{
    private readonly Dictionary<string, string?> _originalEnv = new()
    {
        ["EPG_URL"] = Environment.GetEnvironmentVariable("EPG_URL"),
        ["EPG_URL_FILES"] = Environment.GetEnvironmentVariable("EPG_URL_FILES"),
        ["CHANNEL_MAP_PATH"] = Environment.GetEnvironmentVariable("CHANNEL_MAP_PATH"),
        ["OUTPUT_PATH"] = Environment.GetEnvironmentVariable("OUTPUT_PATH"),
    };

    private void ClearEnv()
    {
        Environment.SetEnvironmentVariable("EPG_URL", null);
        Environment.SetEnvironmentVariable("EPG_URL_FILES", null);
        Environment.SetEnvironmentVariable("CHANNEL_MAP_PATH", null);
        Environment.SetEnvironmentVariable("OUTPUT_PATH", null);
    }

    public void Dispose()
    {
        foreach (var (k, v) in _originalEnv)
            Environment.SetEnvironmentVariable(k, v);
    }

    [Fact]
    public void parses_values_from_args_and_splits_multiple_urls()
    {
        ClearEnv();
        var parser = new ArgumentParser();
        var args = new[]
        {
            "--fake",
            "--url=https://a.com/u1,https://b.com/u2",
            "--channelmap=/tmp/ChannelMap.json",
            "--output=/tmp/out/guide.xml"
        };

        var result = parser.ParseArguments(args);

        result.Fake.Should().BeTrue();
        result.Urls.Should().BeEquivalentTo(new[] { "https://a.com/u1", "https://b.com/u2" });
        result.ChannelMapPath.Should().Be("/tmp/ChannelMap.json");
        result.OutputPath.Should().Be("/tmp/out/guide.xml");
    }

    [Fact]
    public void falls_back_to_environment_variables_when_args_missing()
    {
        ClearEnv();
        Environment.SetEnvironmentVariable("EPG_URL", "https://env.com/u1");
        Environment.SetEnvironmentVariable("CHANNEL_MAP_PATH", "/env/ChannelMap.json");
        Environment.SetEnvironmentVariable("OUTPUT_PATH", "/env/out/guide.xml");

        var parser = new ArgumentParser();
        var result = parser.ParseArguments(Array.Empty<string>());

        result.Urls.Should().BeEquivalentTo(new[] { "https://env.com/u1" });
        result.ChannelMapPath.Should().Be("/env/ChannelMap.json");
        result.OutputPath.Should().Be("/env/out/guide.xml");
    }

    [Fact]
    public void reads_urls_from_epgUrlFiles_ignores_comments_and_blanks()
    {
        ClearEnv();

        // create a temp file with some lines, blanks, and comments
        var tmp = Path.GetTempFileName();
        File.WriteAllLines(tmp, new[]
        {
            "# comment line",
            " https://one.com/epg  ",
            "",
            "https://two.com/epg",
            "   # another comment",
            "   "
        });

        var parser = new ArgumentParser();
        var args = new[]
        {
            $"--epgUrlFiles={tmp}",
            "--channelmap=/tmp/cm.json",
            "--output=/tmp/out.xml"
        };

        var result = parser.ParseArguments(args);

        result.Urls.Should().BeEquivalentTo(new[] { "https://one.com/epg", "https://two.com/epg" });
        result.ChannelMapPath.Should().Be("/tmp/cm.json");
        result.OutputPath.Should().Be("/tmp/out.xml");

        File.Delete(tmp);
    }

    [Fact]
    public void missing_url_from_args_and_env_throws_argument_exception()
    {
        ClearEnv();

        var parser = new ArgumentParser();
        var args = new[]
        {
            "--channelmap=/tmp/ChannelMap.json",
            "--output=/tmp/out/guide.xml"
            // no --url, no EPG_URL, no --epgUrlFiles
        };

        Action act = () => parser.ParseArguments(args);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*URL (--url) must be provided*");
    }

    [Fact]
    public void help_sets_flag_and_returns_help_text_without_exiting()
    {
        var parser = new ArgumentParser();

        var res = parser.ParseArguments(new[] { "--help" });

        res.HelpSet.Should().BeTrue();
        res.HelpText.Should().NotBeNullOrWhiteSpace();
        res.HelpText!.Should().Contain("Usage:");
        res.Urls.Should().BeEmpty(); // nothing else parsed
    }

    [Fact]
    public void url_argument_overrides_epg_url_files_and_environment_values()
    {
        ClearEnv();
        Environment.SetEnvironmentVariable("EPG_URL", "https://env.com/epg");

        var tmp = Path.GetTempFileName();
        File.WriteAllLines(tmp, new[] { "https://file.com/epg" });

        var parser = new ArgumentParser();
        var result = parser.ParseArguments(new[]
        {
            "--URL=https://arg.com/epg",
            $"--epgUrlFiles={tmp}",
            "--output=/tmp/out.xml"
        });

        result.Urls.Should().BeEquivalentTo(new[] { "https://arg.com/epg" });

        File.Delete(tmp);
    }

    [Fact]
    public void reads_urls_from_epg_url_files_environment_variable()
    {
        ClearEnv();

        var tmp = Path.GetTempFileName();
        File.WriteAllLines(tmp, new[] { "https://env-one.com/epg", "https://env-two.com/epg" });
        Environment.SetEnvironmentVariable("EPG_URL_FILES", tmp);
        Environment.SetEnvironmentVariable("OUTPUT_PATH", "/tmp/env-out.xml");

        var parser = new ArgumentParser();
        var result = parser.ParseArguments(Array.Empty<string>());

        result.Urls.Should().BeEquivalentTo(new[] { "https://env-one.com/epg", "https://env-two.com/epg" });
        result.OutputPath.Should().Be("/tmp/env-out.xml");

        File.Delete(tmp);
    }

    [Fact]
    public void uses_default_output_path_and_allows_empty_channel_map()
    {
        ClearEnv();
        var parser = new ArgumentParser();

        var writer = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(writer);

        try
        {
            var result = parser.ParseArguments(new[] { "--url=https://example.com/epg" });

            result.ChannelMapPath.Should().BeEmpty();
            result.OutputPath.Should().Be(Path.Combine(Directory.GetCurrentDirectory(), "output", "guide.xml"));
            writer.ToString().Should().Contain("Warning: No channel map path provided");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void missing_epg_url_file_writes_warning_and_then_throws_for_missing_url()
    {
        ClearEnv();
        var parser = new ArgumentParser();
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");

        var writer = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(writer);

        try
        {
            Action act = () => parser.ParseArguments(new[]
            {
                $"--epgUrlFiles={missingPath}",
                "--output=/tmp/out.xml"
            });

            act.Should().Throw<ArgumentException>()
               .WithMessage("*URL (--url) must be provided*");
            writer.ToString().Should().Contain("The specified EPG URL file does not exist");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
