namespace Crawlers;

/// <summary>
/// Represents a file that has been registered within the system for processing or tracking purposes.
/// </summary>
public class RegisteredFile
{
    public string MimeType { get; set; }
    public List<string> ExtensionList { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }
    public long MaxFileSize { get; set; }
    public bool Supported { get; set;  }

    /// <summary>
    /// Represents a file registered within the system, containing its associated metadata such as
    /// MIME type, supported file extensions, description, icon, and maximum file size.
    /// </summary>
    public RegisteredFile(string mimeType, List<string> extensionList, string description, string icon, long maxFileSize, bool supported)
    {
        MimeType = mimeType;
        ExtensionList = extensionList;
        Description = description;
        Icon = icon;
        MaxFileSize = maxFileSize * 1024L * 1024L;
        Supported = supported;
    }
    
}
