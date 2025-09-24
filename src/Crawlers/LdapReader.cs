namespace Crawlers;

using System.Collections.Generic;
using System.DirectoryServices;
using System.Security.Principal;

/// <summary>
/// A class to read user and group information from an LDAP directory.
/// </summary>
#pragma warning disable CA1416
public class LdapReader
{
    private static readonly RockLogger Logger = RockLogger.GetLogger(typeof(LdapReader));

    private readonly string _adPath;
    private readonly bool _useSSL;
    private readonly string _username;
    private readonly string _password;

    /// <summary>
    /// Initializes a new instance of the LdapReader class.
    /// </summary>
    /// <param name="adPath">The LDAP path (e.g., "DC=yourdomain,DC=com").</param>
    /// <param name="username">The username for binding to LDAP (e.g., "yourdomain\\username" or "username@yourdomain.com").</param>
    /// <param name="password">The password for the specified username.</param>
    public LdapReader(string adPath, bool useSSL, string username, string password)
    {
        _adPath = adPath;
        _useSSL = useSSL;
        _username = username;
        _password = password;
    }

    /// <summary>
    /// Fetches all users from the LDAP directory.
    /// </summary>
    /// <returns>A list of LdapUser objects.</returns>
    public List<LdapUser> GetAllUsers()
    {
        List<LdapUser> users = new List<LdapUser>();
        string ldapPath = _useSSL ? $"LDAPS://CN=Users,{_adPath}" : $"LDAP://CN=Users,{_adPath}";
        try
        {
            using (DirectoryEntry entry = new DirectoryEntry(ldapPath, _username, _password))
            {
                using (DirectorySearcher searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = "(&(objectClass=user)(objectCategory=person))";
                    // Load only necessary properties to improve performance
                    searcher.PropertiesToLoad.Add("distinguishedName");
                    searcher.PropertiesToLoad.Add("sAMAccountName");
                    searcher.PropertiesToLoad.Add("objectSid");
                    searcher.PropertiesToLoad.Add("displayName");
                    searcher.PropertiesToLoad.Add("mail");
                    searcher.PropertiesToLoad.Add("givenName");
                    searcher.PropertiesToLoad.Add("sn");
                    searcher.PropertiesToLoad.Add("memberOf"); // Direct group memberships

                    searcher.PageSize = 1000; // Optimize for large results

                    Logger.Info("Searching for users...");
                    using (SearchResultCollection results = searcher.FindAll())
                    {
                        foreach (SearchResult result in results)
                        {
                            var sid = new SecurityIdentifier((byte[])result.Properties["objectSid"][0], 0);
                            var ntAccount = (NTAccount)sid.Translate(typeof(NTAccount));

                            LdapUser user = new LdapUser
                            {
                                DistinguishedName = GetProperty(result, "distinguishedName"),
                                SamAccountName = GetProperty(result, "sAMAccountName"),
                                DisplayName = GetProperty(result, "displayName"),
                                Email = GetProperty(result, "mail"),
                                FirstName = GetProperty(result, "givenName"),
                                LastName = GetProperty(result, "sn"),
                                Identity = ntAccount.Value.ToLower()
                            };

                            if (user.Email != "" && user.Identity != "")
                            {
                                // Get direct group memberships (returns DNs)
                                if (result.Properties.Contains("memberOf"))
                                {
                                    foreach (string groupDn in result.Properties["memberOf"])
                                    {
                                        user.MemberOfGroups.Add(groupDn);
                                    }
                                }
                                users.Add(user);
                            }
                        }
                    }
                }
            }
            Logger.Info($"Found {users.Count} users with email addresses and identities.");
        }
        catch (DirectoryServicesCOMException ex)
        {
            Logger.Error($"LDAP Error fetching users: {ex.Message}");
            if (ex.ExtendedErrorMessage != null)
            {
                Logger.Error($"Extended Error: {ex.ExtendedErrorMessage}");
            }
        }
        catch (Exception ex2)
        {
            Logger.Error($"An unexpected error occurred while fetching users: {ex2.Message} (bad AD path {ldapPath})");
        }
        return users;
    }

    /// <summary>
    /// Fetches all groups from the LDAP directory.
    /// </summary>
    /// <returns>A list of LdapGroup objects.</returns>
    public List<LdapGroup> GetAllGroups()
    {
        List<LdapGroup> groups = new List<LdapGroup>();
        string ldapPath = _useSSL ? $"LDAPS://CN=Users,{_adPath}" : $"LDAP://CN=Users,{_adPath}";
        try
        {
            using (DirectoryEntry entry = new DirectoryEntry(ldapPath, _username, _password))
            {
                using (DirectorySearcher searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = "(objectClass=group)";
                    searcher.PropertiesToLoad.Add("distinguishedName");
                    searcher.PropertiesToLoad.Add("sAMAccountName");
                    searcher.PropertiesToLoad.Add("displayName");
                    searcher.PropertiesToLoad.Add("objectSid");
                    searcher.PropertiesToLoad.Add("member"); // Direct members (users or other groups)

                    searcher.PageSize = 1000; // Optimize for large results

                    Logger.Info("Searching for groups...");
                    using (SearchResultCollection results = searcher.FindAll())
                    {
                        foreach (SearchResult result in results)
                        {
                            var sid = new SecurityIdentifier((byte[])result.Properties["objectSid"][0], 0);
                            var ntAccount = (NTAccount)sid.Translate(typeof(NTAccount));

                            LdapGroup group = new LdapGroup
                            {
                                DistinguishedName = GetProperty(result, "distinguishedName"),
                                SamAccountName = GetProperty(result, "sAMAccountName"),
                                DisplayName = GetProperty(result, "displayName"),
                                Identity = ntAccount.Value.ToLower()
                            };

                            // Get direct members (returns DNs)
                            if (result.Properties.Contains("member"))
                            {
                                foreach (string memberDn in result.Properties["member"])
                                {
                                    group.Members.Add(memberDn);
                                }
                            }
                            if (group.Identity != "")
                            {
                                groups.Add(group);
                            }
                        }
                    }
                }
            }
            Logger.Info($"Found {groups.Count} groups.");
        }
        catch (DirectoryServicesCOMException ex)
        {
            Logger.Error($"LDAP Error fetching groups: {ex.Message}");
            if (ex.ExtendedErrorMessage != null)
            {
                Logger.Error($"Extended Error: {ex.ExtendedErrorMessage}");
            }
        }
        catch (Exception ex2)
        {
            Logger.Error($"An unexpected error occurred while fetching users: {ex2.Message} (bad AD path {ldapPath})");
        }
        return groups;
    }

    /// <summary>
    /// resolve the members of groups down to users and groups
    /// </summary>
    /// <param name="groupList">the list of groups to resolve</param>
    /// <param name="userResolver">the map of distinguishedUser -> user</param>
    /// <param name="groupResolver">the map of distinguisedGroup -> group</param>
    public void ResolveGroups(List<LdapGroup> groupList, Dictionary<string, LdapUser> userResolver, Dictionary<string, LdapGroup> groupResolver)
    {
        // parent-group -> list of group members
        var groupFlattener = new Dictionary<string, List<string>>();
        var groupLookup = new Dictionary<string, LdapGroup>();
        foreach (var group in groupList)
        {
            var thisGroup = group.DistinguishedName.ToLower();
            groupFlattener[thisGroup] = new List<string>();
            groupLookup[thisGroup] = group;
            var newMemberList = new List<string>();
            foreach (var member in group.Members)
            {
                var memberLwr = member.ToLowerInvariant();
                if (userResolver.TryGetValue(memberLwr, out var user))
                {
                    newMemberList.Add(user.Email);
                }
                else if (groupResolver.TryGetValue(memberLwr, out var group2))
                {
                    groupFlattener[thisGroup].Add(memberLwr);
                }
            }
            group.Members = newMemberList;
        }
        foreach (var key in groupFlattener.Keys)
        {
            if (!groupLookup.ContainsKey(key)) continue;
            var parentGroup = groupLookup[key];
            var groupListInside = groupFlattener[key];
            if (groupListInside != null && groupListInside.Count > 0)
            {
                foreach (var groupInside in groupListInside)
                {
                    if (!groupLookup.ContainsKey(groupInside)) continue;
                    var groupI = groupLookup[groupInside];
                    foreach (var user in groupI.Members)
                    {
                        if (!parentGroup.Members.Contains(user))
                        {
                            parentGroup.Members.Add(user);
                        }
                    }
                }
                Console.WriteLine("test");
            }
        }
    }

    /// <summary>
    /// Helper method to safely retrieve a property value from a SearchResult.
    /// </summary>
    private string GetProperty(SearchResult result, string propertyName)
    {
        if (result.Properties.Contains(propertyName))
        {
            return result.Properties[propertyName][0].ToString() ?? "";
        }
        return "";
    }

    /// <summary>
    /// Helper method to extract sAMAccountName from a Distinguished Name (DN).
    /// This is a simple parsing and might not cover all edge cases.
    /// For robust resolution, you might query LDAP for each DN.
    /// </summary>
    public static string GetSamAccountNameFromDn(string dn)
    {
        if (string.IsNullOrEmpty(dn))
            return "";

        // Example: CN=John Doe,OU=Users,DC=example,DC=com
        // We want 'John Doe' or 'sAMAccountName' if available.
        // A more robust way is to query LDAP for the sAMAccountName attribute of the DN.
        // For simplicity, we'll try to extract from CN=
        var cnMatch = System.Text.RegularExpressions.Regex.Match(dn, @"CN=([^,]+)");
        return cnMatch.Success ? cnMatch.Groups[1].Value :
            // Fallback if CN not found or if it's a different type of DN
            // For example, if it's a userPrincipalName or sAMAccountName directly
            // This is a simplistic approach. For real-world, query LDAP for the object.
            dn; // Return the DN itself if sAMAccountName cannot be parsed
    }
}
#pragma warning restore CA1416
