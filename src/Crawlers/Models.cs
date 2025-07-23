namespace Crawlers;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

// For System.Text.Json's JsonIgnore

public abstract class RenameFolderAsset
{
    [JsonPropertyName("originalFolderName")]
    public string OriginalFolderName { get; set; } = string.Empty;
    [JsonPropertyName("newFolderName")]
    public string NewFolderName { get; set; } = string.Empty;

    [JsonPropertyName("assetAclList")] 
    public List<AssetAcl> AssetAclList { get; set; } = [];

    public RenameFolderAsset() { }
    
    public RenameFolderAsset(string originalFolderName, string newFolderName, List<AssetAcl> assetACLList)
    {
        OriginalFolderName = originalFolderName;
        NewFolderName = newFolderName;
        AssetAclList = assetACLList;
    }
}

public class MetadataMapping
{
    [JsonPropertyName("extMetadata")]
    public string ExtMetadata { get; set; } = string.Empty;
    [JsonPropertyName("metadata")]
    public string Metadata { get; set; } = string.Empty;
    [JsonPropertyName("display")]
    public string Display { get; set; } = string.Empty;

    public MetadataMapping(string extMetadata, string metadata, string display)
    {
        ExtMetadata = extMetadata;
        Metadata = metadata;
        Display = display;
    }
}

// Placeholder for JsonSerializer - needs actual implementation (e.g., using Newtonsoft.Json)
public class JsonSerializer
{
    public static JsonSerializer Create() => new JsonSerializer();
    public string WriteValueAsString(object obj) => System.Text.Json.JsonSerializer.Serialize(obj);
    public T? ReadValue<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json);
}


// Placeholder for Vars - needs actual implementation
public class Vars
{
    public static string Get(string key) => Environment.GetEnvironmentVariable(key) ?? "";
}

// Placeholder for CrawlerUtils - needs actual implementation
public abstract class CrawlerUtils
{
    public static double CrawlerWaitTimeInHours(string currentTimeIndicator, string schedule, bool notRunning) => 0.0; // Placeholder
    public static string GetCurrentTimeIndicatorString() => DateTime.Now.Hour.ToString(); // Placeholder
}

// Placeholder for Sha512 - needs actual implementation
public abstract class Sha512
{
    public static string GenerateSha512Hash(string input) => input; // Placeholder
}

// Placeholder for SharedSecrets - needs actual implementation
public abstract class SharedSecrets
{
    public static Guid GetRandomGuid(int seed) => Guid.NewGuid(); // Placeholder
}
