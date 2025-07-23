namespace Crawlers;
using System.Collections.Generic;

/// <summary>
/// Represents a single entry in a file's Access Control List (ACL).
/// </summary>
public class AccessControlEntry
{
    /// <summary>
    /// The resolved identity (user or group name, e.g., DOMAIN\User or DOMAIN\Group).
    /// </summary>
    public string Identity { get; set; } = "";

    /// <summary>
    /// The type of identity, e.g., "User", "Group", "Well-Known Group", "Unresolved SID".
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// The file system rights granted or denied (e.g., Read, Write, FullControl).
    /// </summary>
    public string FileSystemRights { get; set; } = "";

    /// <summary>
    /// Whether the access rule is 'Allow' or 'Deny'.
    /// </summary>
    public string AccessControlType { get; set; } = "";

    /// <summary>
    /// Indicates if the access rule is inherited from a parent folder.
    /// </summary>
    public bool IsInherited { get; set; }

    /// <summary>
    /// Placeholder for user's email address. Requires Active Directory lookup.
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// Placeholder for group members. Requires Active Directory lookup.
    /// </summary>
    public List<string> GroupMemberships { get; set; } = [];
}
