namespace xmlTVGuide.Services.FileServices;


/// <summary>
/// This interface defines the contract for file services.
/// It includes a method to save a file with the specified content and output path.
/// </summary>
public interface IFileService
{
    bool SaveFile<T>(T content, string outputPath);
}
