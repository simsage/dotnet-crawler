namespace Crawlers;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public class AssetAcl
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("access")]
    public string Access { get; set; } = string.Empty;
    [JsonPropertyName("isUser")]
    public bool IsUser { get; set; } = true;
    [JsonPropertyName("membershipList")]
    public List<string> MembershipList { get; set; } = new List<string>();

    public static string CreateAccessString(bool read, bool write, bool delete) => read ? "R" : "";

    /// <summary>
    /// *****User***** constructor
    /// </summary>
    public AssetAcl(string name, string displayName, string access) :
        this(name, displayName, access, true, new List<string>())
    { }

    /// <summary>
    /// *****Group***** constructor
    /// </summary>
    public AssetAcl(string name, string access, List<string> userMemberList) :
        this(name: name, displayName: "", access: access, isUser: false, membershipList: userMemberList.ToList())
    { }

    public AssetAcl(string name, string displayName, string access, bool isUser, List<string> membershipList)
    {
        Name = name;
        DisplayName = displayName;
        Access = access;
        IsUser = isUser;
        MembershipList = membershipList;
    }

    public AssetAcl() { } // Parameterless constructor for deserialization

    public override string ToString() =>
        !string.IsNullOrEmpty(DisplayName)
            ? $"{Name} ({DisplayName}):{Access}:{(IsUser ? "user" : "group")}"
            : $"{Name}:{Access}:{(IsUser ? "user" : "group")}";

    /// <summary>
    /// Make sure the ACLs are _unique_ and not duplicate
    /// </summary>
    public static List<AssetAcl> UniqueAcls(List<AssetAcl> aclList)
    {
        var seen = new HashSet<string>();
        var filteredList = new List<AssetAcl>();
        foreach (var acl in aclList)
        {
            var aclStr = acl.ToString().ToLowerInvariant();
            if (seen.Contains(aclStr)) continue;
            seen.Add(aclStr);
            filteredList.Add(acl);
        }
        return filteredList.OrderBy(it => it.ToString()).ToList();
    }
}
