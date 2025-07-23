namespace Crawlers;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents metadata and ACL information for a file.
/// </summary>
public class FileMetadata
{
    /// <summary>
    /// The full path to the file on the share.
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// The size of the file in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// The location of the temporary downloaded file.
    /// </summary>
    public string TempFile { get; set; } = "";

    /// <summary>
    /// The last write time of the file.
    /// </summary>
    public DateTime LastWriteTime { get; set; }

    /// <summary>
    /// The created time of the file.
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// A list of access control entries for the file.
    /// </summary>
    public List<AccessControlEntry> AccessControlList { get; set; }

    public FileMetadata()
    {
        AccessControlList = [];
    }
}
