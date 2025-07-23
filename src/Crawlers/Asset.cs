namespace Crawlers;

using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

public class Asset
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    [JsonPropertyName("parentUrl")]
    public string ParentUrl { get; set; } = string.Empty;
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;
    [JsonPropertyName("acls")]
    public List<AssetAcl> Acls { get; set; } = [];
    [JsonPropertyName("deltaRootId")]
    public string DeltaRootId { get; set; } = string.Empty;
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;
    [JsonPropertyName("binarySize")]
    public long BinarySize { get; set; }
    [JsonPropertyName("template")]
    public string Template { get; set; } = string.Empty;
    [JsonPropertyName("created")]
    public long Created { get; set; }
    [JsonPropertyName("lastModified")]
    public long LastModified { get; set; }
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;
    [JsonPropertyName("previewImage")]
    public string PreviewImage { get; set; } = string.Empty;

    public Asset() { }

    public override string ToString() =>
        $"Asset(url={Url},filename=\"{Filename}\",metadata={Metadata.Count},mimeType={MimeType})";

    
    public static bool IsFile(FileInfo fileInfo)
    {
        return (fileInfo.Attributes & FileAttributes.Directory) == 0;
    }
    
    /// <summary>
    /// read the contents of filename if it exists and remove filename after reading
    /// </summary>
    public byte[] ReadBytesAndRemoveFile()
    {
        if (!string.IsNullOrEmpty(Filename))
        {
            var tempFile = new FileInfo(Filename);
            if (tempFile.Exists && IsFile(tempFile)) // IsFile() needs to be an extension method or check FileAttributes
            {
                var data = File.ReadAllBytes(Filename);
                tempFile.Delete();
                return data;
            }
        }
        return [];
    }

    /// <summary>
    /// remove the temp file if it exists and set it to empty
    /// </summary>
    public void RemoveAssetTempFile()
    {
        if (!string.IsNullOrEmpty(Filename))
        {
            var tempFile = new FileInfo(Filename);
            if (tempFile.Exists && IsFile(tempFile))
            {
                tempFile.Delete();
            }

            Filename = "";
        }
    }

    /// <summary>
    /// read the contents of filename if it exists, null otherwise
    /// </summary>
    public byte[] ReadBytes()
    {
        if (string.IsNullOrEmpty(Filename)) return [];
        var tempFile = new FileInfo(Filename);
        return !tempFile.Exists ? [] : File.ReadAllBytes(Filename);
    }

    /// <summary>
    /// Calculates a hash value based on the current properties of the asset
    /// and its associated metadata, access control list, and binary data.
    /// </summary>
    /// <returns>A string representing the computed hash value.</returns>
    public string CalculateHash()
    {
        var aclStr = string.Join(",", Acls.Select(acl => acl.ToString())); 
        var metadataStr = string.Join(
            ",",
            Metadata.OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}={pair.Value}")
        );
        return Md5Hasher.ComputeCombinedHash(
            Url, 
            aclStr, 
            metadataStr, 
            BinarySize.ToString(), 
            LastModified.ToString(), 
            Author, 
            MimeType, 
            ReadBytes()
            );
    }
    
}
