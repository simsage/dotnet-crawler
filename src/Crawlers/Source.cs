namespace Crawlers;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class Source : IComparable<Source>
{
    // the id of this crawler and the source-id
    [JsonPropertyName("sourceId")]
    public int SourceId { get; set; }

    // the organisation owner
    [JsonPropertyName("organisationId")]
    public string OrganisationId { get; set; } = "";

    // the kb owner
    [JsonPropertyName("kbId")]
    public string KbId { get; set; } = "";

    // the importance (weight) of this source <0.0, 1.0]
    [JsonPropertyName("weight")]
    public float Weight { get; set; } = 1.0f;

    // the system's node id of this source (what machine to run on)
    [JsonPropertyName("nodeId")]
    public int NodeId { get; set; } = 0;

    // name of the crawler
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    // the id of the system
    [JsonPropertyName("crawlerType")]
    public string CrawlerType { get; set; } = "";

    // the time schedule for this crawler
    [JsonPropertyName("schedule")]
    public string Schedule { get; set; } = "";

    // delete files after crawl complete?
    [JsonPropertyName("deleteFiles")]
    public bool DeleteFiles { get; set; } = false;

    // does this source allow anonymous access?
    [JsonPropertyName("allowAnonymous")]
    public bool AllowAnonymous { get; set; } = true;

    // the processing level for this source
    [JsonPropertyName("processingLevel")]
    public string ProcessingLevel { get; set; } = "INDEX";

    // does this source need image preview images?
    [JsonPropertyName("enablePreview")]
    public bool EnablePreview { get; set; } = false;

    // SM-899 - now the number of milliseconds delay between uploads
    [JsonPropertyName("filesPerSecond")]
    public float FilesPerSecond { get; set; } = 0.0f;

    // json specific to the type of the crawler
    [JsonPropertyName("specificJson")]
    public string SpecificJson { get; set; } = "";

    // for returning data
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    // maximum number of items allowed if > 0
    [JsonPropertyName("maxItems")]
    public long MaxItems { get; set; } = 0L;

    // maximum number of bot items (for WP crawlers)
    [JsonPropertyName("maxBotItems")]
    public long MaxBotItems { get; set; } = 0L;

    // custom rendering (for DB and Restful sources if enabled, otherwise false)
    [JsonPropertyName("customRender")]
    public bool CustomRender { get; set; } = false;

    // associated edge device id (if set)
    [JsonPropertyName("edgeDeviceId")]
    public string EdgeDeviceId { get; set; } = "";

    // when the crawler start processing
    [JsonPropertyName("startTime")]
    public long StartTime { get; set; } = 0L;

    // when the crawler successfully finished processing
    [JsonPropertyName("endTime")]
    public long EndTime { get; set; } = 0L;

    // when the crawler successfully finished optimizing
    [JsonPropertyName("optimizedTime")]
    public long OptimizedTime { get; set; } = 0L;

    // number of errors found in a processing run
    [JsonPropertyName("numErrors")]
    public int NumErrors { get; set; } = 0;

    // threshold for successful / fail number of errors (if > 0)
    [JsonPropertyName("errorThreshold")]
    public int ErrorThreshold { get; set; } = 0;

    // expand body relationships?  (default true)
    [JsonPropertyName("useDefaultRelationships")]
    public bool UseDefaultRelationships { get; set; } = true;

    // use OCR in processing documents of this source?
    [JsonPropertyName("useOCR")]
    public bool UseOcr { get; set; } = true;

    // use Speech to text for this source?
    [JsonPropertyName("useSTT")]
    public bool UseStt { get; set; } = true;

    // store the document-binaries for this source?
    [JsonPropertyName("storeBinary")]
    public bool StoreBinary { get; set; } = true;

    // write direct to Cassandra
    [JsonPropertyName("writeToCassandra")]
    public bool WriteToCassandra { get; set; } = true;

    // inherited security
    [JsonPropertyName("acls")]
    public List<DocumentAcl> Acls { get; set; } = [];

    // default number of results returned
    [JsonPropertyName("numResults")]
    public int NumResults { get; set; } = 5;

    // default number of fragments in search results
    [JsonPropertyName("numFragments")]
    public int NumFragments { get; set; } = 3;

    [JsonPropertyName("processorConfig")]
    public string ProcessorConfig { get; set; } = "";

    [JsonPropertyName("documentSimilarityThreshold")]
    public float DocumentSimilarityThreshold { get; set; } = 0.9f;  // the threshold for these calculations
    [JsonPropertyName("enableDocumentSimilarity")]
    public bool EnableDocumentSimilarity { get; set; } = true;     // perform document similarity calculations?
    [JsonPropertyName("translateForeignLanguages")]
    public bool TranslateForeignLanguages { get; set; } = false;   // translate supported foreign languages

    // is this an external source? (forced)
    [JsonPropertyName("isExternal")]
    public bool IsExternal { get; set; }

    // the delta indicator for this source (optional) - where the source is at for remote systems
    [JsonPropertyName("deltaIndicator")]
    public string DeltaIndicator { get; set; } = "";

    // set after deltas were reset to delete unseen files
    [JsonPropertyName("deltaResetCrawl")]
    public bool DeltaResetCrawl { get; set; } = false;

    [JsonPropertyName("deltaResetRoots")]
    public List<string> DeltaResetRoots { get; set; } = new List<string>();

    // if true, write external crawler logs to SimSage
    [JsonPropertyName("transmitExternalLogs")]
    public bool TransmitExternalLogs { get; set; } = false;

    [JsonPropertyName("scheduleEnable")]
    public bool ScheduleEnable { get; set; } = true;

    // does this source return results by newest first?
    [JsonPropertyName("sortByNewestFirst")]
    public bool SortByNewestFirst { get; set; } = false;

    // does this source have any delta values?  including delta roots
    [JsonPropertyName("hasDeltaValues")]
    public bool HasDeltaValues { get; set; } = false;

    // when the crawler finished (not all processing, just the crawler itself)
    [JsonPropertyName("endTimeCrawler")]
    public long EndTimeCrawler { get; set; } = 0L;

    // mimetypes that are inventory only (i.e., not to be processed)
    [JsonPropertyName("inventoryOnlyInclude")]
    public bool InventoryOnlyInclude { get; set; } = false; // if true, inclusive, otherwise exclusive
    
    [JsonPropertyName("inventoryOnlyMimeTypes")]
    public List<string> InventoryOnlyMimeTypes { get; set; } = new List<string>();

    /// <summary>
    /// @return crawler ID
    /// </summary>
    public override string ToString() => $"{KbId}:{SourceId}";

    // is the given mimeType part of the inventory only mimeTypes for this source?
    public bool IsInventoryOnly(string mimeType)
    {
        // empty mime-types cannot be processed - so they go straight into the inventory
        if (mimeType.Trim().Length == 0) return true;
        var index = mimeType.IndexOf(';');
        var inList = index > 0
            ? InventoryOnlyMimeTypes.Contains(mimeType.Substring(0, index).Trim().ToLowerInvariant())
            : InventoryOnlyMimeTypes.Contains(mimeType.Trim().ToLowerInvariant());
        // so it is NOT in the list, AND we're in include mode, then we are NOT inventory-only
        return InventoryOnlyInclude ? !inList : inList;
    }

    /// <summary>
    /// read a json item from the specific json attribute
    /// </summary>
    public object? SpecificJsonProperty(string name)
    {
        if (!string.IsNullOrEmpty(SpecificJson))
        {
            // make sure this user is right for this source (userList)
            var map = GetCrawlerPropertyMap(SpecificJson);
            if (map.ContainsKey(name))
            {
                return map[name].ToString();
            }
        }
        return null;
    }

    /// <summary>
    /// write a specific item into the specific json attribute
    /// </summary>
    public void SetSpecificJsonProperty(string name, string value)
    {
        var map = string.IsNullOrEmpty(SpecificJson)
            ? new Dictionary<string, object>()
            : GetCrawlerPropertyMap(SpecificJson);
        map[name] = value;
        SpecificJson = JsonSerializer.Create().WriteValueAsString(map);
    }

    /// <summary>
    /// Access the crawler's specific _json map_
    /// </summary>
    public Dictionary<string, object> GetCrawlerPropertyMap() => GetCrawlerPropertyMap(SpecificJson);

    public int CompareTo(Source? other) => String.Compare(Name, other?.Name, StringComparison.Ordinal);

    // crawler types
    // ReSharper disable MemberHidesStaticFromOuterClass
    // ReSharper disable InconsistentNaming
    public const string CT_WEB = "web";
    public const string CT_FILE = "file";
    public const string CT_DATABASE = "database";
    public const string CT_EXTERNAL = "external";              // SimSage API crawler
    public const string CT_EXCHANGE365 = "exchange365";
    public const string CT_ONEDRIVE = "onedrive";
    public const string CT_SHAREPOINT365 = "sharepoint365";
    public const string CT_DROPBOX = "dropbox";
    public const string CT_DISCOURSE = "discourse";
    public const string CT_GOOGLE_DRIVE = "gdrive";
    public const string CT_JIRA = "jira";
    public const string CT_LOCAL_FILE = "localfile";
    public const string CT_RESTFULL = "restfull";
    public const string CT_RSS = "rss";
    public const string CT_DMS = "dms";
    public const string CT_BOX = "box";
    public const string CT_IMANAGE = "imanage";
    public const string CT_SERVICE_NOW = "servicenow";
    public const string CT_SEARCH = "search";
    public const string CT_CONFLUENCE = "confluence";
    public const string CT_STRUCTURED = "structured";
    public const string CT_AWS = "aws";
    public const string CT_EGNYTE = "egnyte";
    public const string CT_SFTP = "sftp";
    public const string CT_ZENDESK = "zendesk";
    public const string CT_SLACK = "slack";
    public const string CT_XML = "xml";
    public const string CT_ALFRESCO = "alfresco";
    public const string CT_ARCGIS = "arc";

    // the name of the refresh token property
    public const string REFRESH_TOKEN = "refreshToken";

    // Source-types that support delta crawling
    public static readonly HashSet<string> DeltaCrawlerSet =
        [CT_EXCHANGE365, CT_SHAREPOINT365, CT_DROPBOX, CT_BOX, CT_GOOGLE_DRIVE, CT_DISCOURSE, CT_EGNYTE];

    /// <summary>
    /// Process a crawler's specific property map
    /// @return the map
    /// </summary>
    public static Dictionary<string, object> GetCrawlerPropertyMap(string specificJson)
    {
        if (!string.IsNullOrEmpty(specificJson) && specificJson.Trim() != "{}")
        {
            var mapper = JsonSerializer.Create();
            try
            {
                return mapper.ReadValue<Dictionary<string, object>>(specificJson) ?? new Dictionary<string, object>();
            }
            catch (System.Text.Json.JsonException) // Equivalent to JsonParseException
            {
                // fix double \\ if they are a problem
                return mapper.ReadValue<Dictionary<string, object>>(specificJson.Replace("\\\\", "\\")) ?? new Dictionary<string, object>();
            }
        }
        return new Dictionary<string, object>();
    }

    /// <summary>
    /// return true if value is AES encrypted by SimSAge
    /// @param value the value to check
    /// @return true if the value has been encrypted by SimSage
    /// </summary>
    public static bool IsEncrypted(string value)
    {
        return value.Replace("\\n", "\n").Trim().StartsWith(AesEncryption.AesPrePost);
    }

}
