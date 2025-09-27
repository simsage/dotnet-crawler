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
            throw;
        }
        catch (Exception ex2)
        {
            Logger.Error($"An unexpected error occurred while fetching users: {ex2.Message} (bad AD path {ldapPath})");
            throw;
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
            throw;
        }
        catch (Exception ex2)
        {
            Logger.Error($"An unexpected error occurred while fetching users: {ex2.Message} (bad AD path {ldapPath})");
            throw;
        }
        return groups;
    }


    /// <summary>
    /// Resolves the members of groups, flattening nested group memberships down to a final list of unique users.
    /// This method modifies the 'ResolvedUsers' property of each LdapGroup object in the provided list.
    /// </summary>
    /// <param name="groupList">The list of top-level groups to resolve.</param>
    /// <param name="userResolver">A dictionary mapping a user's distinguished name to the LdapUser object.</param>
    /// <param name="groupResolver">A dictionary mapping a group's distinguished name to the LdapGroup object.</param>
    public void ResolveGroups(
        List<LdapGroup> groupList,
        Dictionary<string, LdapUser> userResolver,
        Dictionary<string, LdapGroup> groupResolver
    )
    {
        // A cache to store the fully resolved user list for a group. This prevents
        // re-resolving the same group multiple times if it's nested in different parent groups.
        var resolutionCache = new Dictionary<string, HashSet<LdapUser>>();

        // Iterate through each top-level group and resolve its members.
        foreach (var group in groupList)
        {
            // The main recursive logic is in the helper function. We start with an empty path for each top-level group.
            var resolvedUsers = GetResolvedUsersRecursive(group, userResolver, groupResolver, resolutionCache, new HashSet<string>());
            group.Members = resolvedUsers.Select(p => p.Email).ToList();
        }
    }

    /// <summary>
    /// A private helper method that recursively finds all unique users in a group and its subgroups.
    /// </summary>
    /// <param name="currentGroup">The group currently being resolved.</param>
    /// <param name="userResolver">The master dictionary of all users.</param>
    /// <param name="groupResolver">The master dictionary of all groups.</param>
    /// <param name="cache">The cache for storing results of already-resolved groups.</param>
    /// <param name="currentPath">A set tracking the groups visited in the current recursive path to detect cycles.</param>
    /// <returns>A HashSet of unique LdapUser objects.</returns>
    private HashSet<LdapUser> GetResolvedUsersRecursive(
        LdapGroup currentGroup,
        Dictionary<string, LdapUser> userResolver,
        Dictionary<string, LdapGroup> groupResolver,
        Dictionary<string, HashSet<LdapUser>> cache,
        HashSet<string> currentPath)
    {
        // 1. Performance Check: If this group has been resolved before, return the cached result immediately.
        if (cache.TryGetValue(currentGroup.DistinguishedName, out var cachedUsers))
        {
            return cachedUsers;
        }

        // 2. Cycle Detection: If we have already seen this group in the current resolution path,
        // we have a circular dependency (e.g., GroupA contains GroupB, and GroupB contains GroupA).
        // Return an empty set to break the infinite loop.
        if (!currentPath.Add(currentGroup.DistinguishedName))
        {
            return new HashSet<LdapUser>(); // Cycle detected
        }

        var allUsers = new HashSet<LdapUser>();

        // 3. Process Members: Iterate through the distinguished names of the group's members.
        foreach (var memberDn in currentGroup.Members)
        {
            // Case A: The member is a user.
            if (userResolver.TryGetValue(memberDn, out var user))
            {
                allUsers.Add(user);
            }
            // Case B: The member is another group.
            else if (groupResolver.TryGetValue(memberDn, out var subGroup))
            {
                // Recursively call this function to resolve the subgroup.
                var usersFromSubGroup = GetResolvedUsersRecursive(subGroup, userResolver, groupResolver, cache, currentPath);
                
                // Add the users from the subgroup into our main set.
                // UnionWith is efficient and handles duplicates automatically.
                allUsers.UnionWith(usersFromSubGroup);
            }
        }

        // 4. Backtrack: After processing all members of this group, remove it from the current path.
        // This allows it to be resolved again if encountered through a different parent group.
        currentPath.Remove(currentGroup.DistinguishedName);

        // 5. Cache Result: Store the final set of resolved users in the cache before returning.
        cache[currentGroup.DistinguishedName] = allUsers;

        return allUsers;
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
