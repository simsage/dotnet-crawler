namespace Crawlers;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public class UploadExternalDocumentCmd
{
    [JsonPropertyName("organisationId")]
    public string OrganisationId { get; set; } = "";
    [JsonPropertyName("kbId")]
    public string KbId { get; set; } = "";
    [JsonPropertyName("sid")]
    public string Sid { get; set; } = "";
    [JsonPropertyName("sourceId")]
    public int SourceId { get; set; } = 0;
    [JsonPropertyName("deltaRootId")]
    public string DeltaRootId { get; set; } = "";
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "";
    [JsonPropertyName("puid")]
    public string Puid { get; set; } = "";
    [JsonPropertyName("acls")]
    public List<AssetAcl> Acls { get; set; } = [];
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
    [JsonPropertyName("author")]
    public string Author { get; set; } = "";
    [JsonPropertyName("changeHash")]
    public string ChangeHash { get; set; } = "";             // document change detection
    [JsonPropertyName("contentHash")]
    public string ContentHash { get; set; } = "";            // document exact duplicate detection
    [JsonPropertyName("generatePreview")]
    public bool GeneratePreview { get; set; } = true;      // override for individual documents if needed
    [JsonPropertyName("binarySize")]
    public long BinarySize { get; set; }                    // just in case the binary is empty, we can still have its size
    [JsonPropertyName("template")]
    public string Template { get; set; } = "";              // an html render template for database like items
    [JsonPropertyName("created")]
    public long Created { get; set; }
    [JsonPropertyName("lastModified")]
    public long LastModified { get; set; }
    [JsonPropertyName("size")]
    public long Size { get; set; } = 0L;
    [JsonPropertyName("inventoryOnly")]
    public bool InventoryOnly { get; set; } = false;
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();          // assorted metadata
    [JsonPropertyName("categories")]
    public List<MetadataMapWithValues> Categories { get; set; } = [];  // categorical stuff
    [JsonPropertyName("runId")]
    public long RunId { get; set; } = 0L;
    [JsonPropertyName("parentUrl")]
    public string ParentUrl { get; set; } = "";
    [JsonPropertyName("processAssetReturn")]
    public bool ProcessAssetReturn { get; set; } = true;    // return value from process asset

    public override string ToString()
    {
        return $"UploadExternalDocumentCmd(runId={RunId},org={OrganisationId},kb={KbId},source={SourceId}," +
               $"url={Url},metadata.size={Metadata.Count},puid={Puid})";
    }

    // convert an asset to an external upload document command
    public static UploadExternalDocumentCmd Convert(Asset asset)
    {
        var obj = new UploadExternalDocumentCmd();
        obj.Url = asset.Url;
        obj.DeltaRootId = asset.DeltaRootId;
        obj.ParentUrl = asset.ParentUrl;
        obj.MimeType = asset.MimeType;
        obj.Title = asset.Title;
        obj.Author = asset.Author;
        obj.BinarySize = asset.BinarySize;
        obj.Template = asset.Template;
        obj.Created = asset.Created;
        obj.LastModified = asset.LastModified;
        obj.Metadata = asset.Metadata;
        obj.ParentUrl = asset.ParentUrl;
        // careful this straight conversion doesn't create users nor groups in SimSage
        foreach (var acl in asset.Acls)
        {
            obj.Acls.Add(acl);
        }
        return obj;
    }

}

