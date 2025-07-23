namespace Crawlers;

using System.Collections.Generic;

/// <summary>
/// Represents a group object found in LDAP.
/// </summary>
public class LdapGroup
{
    public string DistinguishedName { get; set; } = "";
    public string SamAccountName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    /// <summary>
    /// A list of Distinguished Names (DNs) of the direct members (users or other groups) of this group.
    /// </summary>
    public List<string> Members { get; set; } = new List<string>();

    public override string ToString()
    {
        return $"Group: {DisplayName} ({SamAccountName}) - DN: {DistinguishedName}";
    }
}

