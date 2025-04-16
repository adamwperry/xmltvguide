using System;
using System.IO;
using System.Xml.Linq;

namespace xmlTVGuide.Services.FileServices;

/// <summary>
/// This class is responsible for saving XML files.
/// It implements the IFileService interface.
/// The SaveFile method saves the provided XML content to the specified output path.
/// </summary>
/// <typeparam name="T"></typeparam>
public class XMLFileService<T> : IFileService
where T : XDocument
{
    /// <summary>
    /// Saves the provided XML content to the specified output path.
    /// </summary>
    /// <typeparam name="T">XDocument</typeparam>
    /// <param name="content">XDocument content to be saved.</param>
    /// <param name="outputPath">The path where the XML file will be saved.</param>
    /// <returns>Returns true if the file was saved successfully; otherwise, false.</returns>
    public bool SaveFile<T>(T content, string outputPath)
    {
        if (content is not XDocument doc)
        {
            Console.WriteLine("Error: Content is not of type XDocument.");
            return false;
        }

        try
        {
            // Ensure the directory exists before saving
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            doc.Save(outputPath);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Access denied: {ex.Message}");
            return false;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"I/O error: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
            return false;
        }
    }
}
