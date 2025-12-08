using FluentAssertions;
using xmlTVGuide.Services.ChannelMap;
using Xunit;

namespace XmlTvGuide.Generator.Tests;

public class ChannelMapLoaderTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly ChannelMapLoader _channelMapLoader;
    private readonly List<string> _tempFiles;

    public ChannelMapLoaderTests()
    {
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
        _channelMapLoader = new ChannelMapLoader();
        _tempFiles = new List<string>();
    }

    #region Basic Functionality Tests

    [Fact]
    public void LoadChannelMap_WithValidJsonFile_ReturnsCorrectChannelList()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "valid-channel-map.json");

        // Act
        var result = _channelMapLoader.LoadChannelMap(filePath);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        
        result[0].Name.Should().Be("APW Sports");
        result[0].ChannelId.Should().Be("10179");
        
        result[1].Name.Should().Be("AWP");
        result[1].ChannelId.Should().Be("10142");
        
        result[2].Name.Should().Be("WIP");
        result[2].ChannelId.Should().Be("12500");
    }

    [Fact]
    public void LoadChannelMap_WithEmptyChannelsArray_ReturnsEmptyList()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "empty-channels.json");

        // Act
        var result = _channelMapLoader.LoadChannelMap(filePath);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void LoadChannelMap_WithNullFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => _channelMapLoader.LoadChannelMap(null!);
        action.Should().Throw<ArgumentException>()
              .WithMessage("File path cannot be null or empty.*")
              .And.ParamName.Should().Be("filePath");
    }

    [Fact]
    public void LoadChannelMap_WithEmptyFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => _channelMapLoader.LoadChannelMap("");
        action.Should().Throw<ArgumentException>()
              .WithMessage("File path cannot be null or empty.*")
              .And.ParamName.Should().Be("filePath");
    }

    [Fact]
    public void LoadChannelMap_WithWhitespaceFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => _channelMapLoader.LoadChannelMap("   ");
        action.Should().Throw<ArgumentException>()
              .WithMessage("File path cannot be null or empty.*")
              .And.ParamName.Should().Be("filePath");
    }

    [Fact]
    public void LoadChannelMap_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFilePath = Path.Combine(_testDataPath, "non-existent-file.json");

        // Act & Assert
        var action = () => _channelMapLoader.LoadChannelMap(nonExistentFilePath);
        action.Should().Throw<FileNotFoundException>()
              .WithMessage($"The file '{nonExistentFilePath}' does not exist.");
    }

    [Fact]
    public void LoadChannelMap_WithInvalidJson_ThrowsJsonException()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "invalid-json.json");

        // Act & Assert
        var action = () => _channelMapLoader.LoadChannelMap(filePath);
        action.Should().Throw<System.Text.Json.JsonException>()
              .WithMessage("*invalid start of a value*");
    }

    [Fact]
    public void LoadChannelMap_WithMissingChannelsKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "missing-channels-key.json");

        // Act & Assert
        var action = () => _channelMapLoader.LoadChannelMap(filePath);
        action.Should().Throw<InvalidOperationException>()
              .WithMessage("Missing 'channels' in channel map.");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void LoadChannelMap_WithMalformedChannels_FiltersOutInvalidEntries()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "malformed-channels.json");

        // Act
        var result = _channelMapLoader.LoadChannelMap(filePath);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty(); // All entries should be filtered out due to missing/empty names or channelIds
    }

    [Fact]
    public void LoadChannelMap_WithMixedValidAndInvalidChannels_ReturnsOnlyValidChannels()
    {
        // Arrange
        var testJson = @"{
            ""channels"": [
                { ""channel"": { ""name"": ""Valid Channel"", ""channelId"": ""12345"" } },
                { ""channel"": { ""name"": """", ""channelId"": ""67890"" } },
                { ""channel"": { ""name"": ""Another Valid"", ""channelId"": ""11111"" } },
                { ""channel"": { ""name"": ""Invalid"", ""channelId"": """" } },
                { ""channel"": { ""channelId"": ""22222"" } }
            ]
        }";
        var tempFilePath = CreateTempJsonFile(testJson);

        // Act
        var result = _channelMapLoader.LoadChannelMap(tempFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        
        result[0].Name.Should().Be("Valid Channel");
        result[0].ChannelId.Should().Be("12345");
        
        result[1].Name.Should().Be("Another Valid");
        result[1].ChannelId.Should().Be("11111");
    }

    [Fact]
    public void LoadChannelMap_WithNullChannelProperties_FiltersOutNullEntries()
    {
        // Arrange
        var testJson = @"{
            ""channels"": [
                { ""channel"": { ""name"": ""Valid Channel"", ""channelId"": ""12345"" } },
                { ""channel"": null },
                { ""channel"": { ""name"": ""Another Valid"", ""channelId"": ""67890"" } }
            ]
        }";
        var tempFilePath = CreateTempJsonFile(testJson);

        // Act
        var result = _channelMapLoader.LoadChannelMap(tempFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        
        result[0].Name.Should().Be("Valid Channel");
        result[0].ChannelId.Should().Be("12345");
        
        result[1].Name.Should().Be("Another Valid");
        result[1].ChannelId.Should().Be("67890");
    }

    [Fact]
    public void LoadChannelMap_WithEmptyJsonObject_ThrowsInvalidOperationException()
    {
        // Arrange
        var testJson = "{}";
        var tempFilePath = CreateTempJsonFile(testJson);

        // Act & Assert
        var action = () => _channelMapLoader.LoadChannelMap(tempFilePath);
        action.Should().Throw<InvalidOperationException>()
              .WithMessage("Missing 'channels' in channel map.");
    }

    [Fact]
    public void LoadChannelMap_WithChannelsNotArray_ThrowsInvalidOperationException()
    {
        // Arrange
        var testJson = @"{
            ""channels"": ""not an array""
        }";
        var tempFilePath = CreateTempJsonFile(testJson);

        // Act & Assert
        var action = () => _channelMapLoader.LoadChannelMap(tempFilePath);
        action.Should().Throw<InvalidOperationException>()
              .WithMessage("The node must be of type 'JsonArray'.");
    }

    #endregion

    #region Helper Methods

    private string CreateTempJsonFile(string jsonContent)
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json");
        File.WriteAllText(tempFilePath, jsonContent);
        _tempFiles.Add(tempFilePath);
        return tempFilePath;
    }

    #endregion

    #region Cleanup

    public void Dispose()
    {
        // Clean up any temporary files created during tests
        foreach (var tempFile in _tempFiles)
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #endregion
}