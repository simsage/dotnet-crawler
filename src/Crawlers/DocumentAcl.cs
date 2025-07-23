namespace Crawlers;

// Placeholder for DocumentAcl - needs actual implementation

using System;
using System.Text.Json.Serialization;

public class DocumentAcl : IComparable<DocumentAcl>
{
    [JsonPropertyName("acl")]
    public string Acl { get; set; } = string.Empty;
    [JsonPropertyName("isUser")]
    public bool IsUser { get; set; } = true;

    public int CompareTo(DocumentAcl? other)
    {
        return Acl.CompareTo(other?.Acl ?? "");
    }

    public DocumentAcl(string Acl, bool IsUser)
    {
        this.Acl = Acl;
        this.IsUser = IsUser;
    }
    
    public override string ToString()
    {
        return $"ACL: {Acl}, IsUser: {IsUser}";
    }
}

