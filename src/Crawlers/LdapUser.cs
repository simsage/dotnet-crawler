namespace Crawlers;

using System.Collections.Generic;

/// <summary>
/// Represents a user object found in LDAP.
/// </summary>
public class LdapUser
{
    public string DistinguishedName { get; set; } = "";
    public string SamAccountName { get; set; } = "";
    public string Identity { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    /// <summary>
    /// A list of Distinguished Names (DNs) of the groups this user is a direct member of.
    /// </summary>
    public List<string> MemberOfGroups { get; set; } = [];

    public override string ToString()
    {
        return $"User: {DisplayName} ({SamAccountName}) - Email: {Email} - DN: {DistinguishedName}";
    }
}
