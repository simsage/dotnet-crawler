namespace Crawlers;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

/// <summary>
/// Microsoft File Share Crawler
/// </summary>
#pragma warning disable CA1416
public class MicrosoftFileShareCrawler : ICrawler
{
    private readonly RockLogger _logger = RockLogger.GetLogger(typeof(MicrosoftFileShareCrawler));

    // the connection to the remote server
    public bool Active { get; set; } = true;

    private readonly CultureInfo
        _formatter = CultureInfo.InvariantCulture; // Use InvariantCulture for consistent formatting

    private readonly AesEncryption _aes = new AesEncryption();
    private ICrawlerApi? _api; // Nullable
    private string _name = ""; // source's name
    private string _shareStartPath = ""; // where the server starts
    private Dictionary<string, object> _propertyMap = new Dictionary<string, object>();
    private Dictionary<string, LdapUser> _adUsers = new Dictionary<string, LdapUser>();
    private Dictionary<string, LdapGroup> _adGroups = new Dictionary<string, LdapGroup>();

    // the epoch time for calculations
    private readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /*********************************************-MAIN CRAWLER-*********************************************/

    /// <summary>
    /// initialize this crawler
    /// </summary>
    /// <param name="name">the name of this crawler</param>
    /// <param name="propertyMap">the properties this crawler needs to function</param>
    /// <param name="api">the api to call for communicating with SimSage</param>
    public void Initialize(string name, Dictionary<string, object> propertyMap, ICrawlerApi api)
    {
        _name = name;
        _propertyMap = new Dictionary<string, object>(propertyMap);
        _api = api;
    }


    /// <summary>
    /// do the crawl, go into folder recursively and get all the files and folders inside
    /// and traverse through them one at a time
    /// </summary>
    /// <returns><c>true</c> if crawler runs as _expected_.</returns>
    public bool Run()
    {
        _logger.Info($"{_name}: file crawler starting");
        Test(); // make sure we can connect before starting

        var parameters = GetParameters();

        // get AD information if set up
        var (users, groups) = SetupAdUsersAndGroups(parameters);
        _adUsers = users;
        _adGroups = groups;

        // do it and return the exit status
        return CrawlDirectory(_shareStartPath, 0);
    }

    public void SetDeltaState(string deltaState)
    {
    }

    /// <summary>
    /// testing connectivity of crawler.
    /// note - unexpected behaviour is _not_ necessarily handled in _every_ case here.
    /// </summary>
    /// <returns><c>true</c> if connection successful</returns>
    public bool Test()
    {
        // establish a connection
        GetParameters();
        return true;
    }

    public void UpdateRefreshToken(string refreshToken)
    {
        _propertyMap[Source.REFRESH_TOKEN] = refreshToken.Trim();
    }

    /// <summary>
    /// Parameters of this crawler
    /// </summary>
    private record CrawlerParameterSet(
        string Username,
        string Password,
        bool UseAd,
        string AdPath,
        bool UseSsl
    );

    /// <summary>
    /// Parse the parameters into a neat CrawlerParameterSet object
    /// </summary>
    private CrawlerParameterSet GetParameters()
    {
        if (_api == null)
            throw new Exception($"{_name}: API is null");

        // Check that the necessary properties exist
        _api.VerifyParameters(_name, _propertyMap, ["server", "username", "password", "shareName"]);

        // share details
        var sharePassword = _propertyMap.TryGetValue("password", out var pwdObj) ? pwdObj.ToString() ?? "" : "";
        var shareServer = _propertyMap.TryGetValue("server", out var serverObj) ? serverObj.ToString() ?? "" : "";
        if (shareServer.Trim().Length == 0)
        {
            throw new Exception($"{_name}: server is empty");
        }
        var password = Source.IsEncrypted(sharePassword) ? _aes.Decrypt(sharePassword) : sharePassword;
        var username = _propertyMap.TryGetValue("username", out var userObj) ? userObj.ToString() ?? "" : "";
        var shareName = _propertyMap.TryGetValue("shareName", out var shareNameObj)
            ? shareNameObj.ToString() ?? ""
            : "";
        if (shareName.Trim().Length == 0)
        {
            throw new Exception($"{_name}: share-name is empty");
        }
        var sharePath = _propertyMap.TryGetValue("sharePath", out var sharePathObj)
            ? sharePathObj.ToString() ?? ""
            : "";

        _shareStartPath = sharePath.Length > 0 ? $@"\\{shareServer}\{shareName}\{sharePath}" : $@"\\{shareServer}\{shareName}";

        // Check if the user wants to use an active directory (optional)
        var useAd = (_propertyMap.TryGetValue("useAD", out var useAdObj) ? useAdObj.ToString() ?? "false" : "false")
            .ToLowerInvariant().Trim() == "true";
        var useSsl =
            (_propertyMap.TryGetValue("useSSL", out var useSslObj) ? useSslObj.ToString() ?? "false" : "false")
            .ToLowerInvariant().Trim() == "true";
        var adPath = _propertyMap.TryGetValue("adPath", out var adPathObj) ? adPathObj.ToString() ?? "" : "";

        if (useAd)
        {
            _api.VerifyParameters(_name, _propertyMap, ["adPath", "username", "password"]);
        }

        return new CrawlerParameterSet(username, password, useAd, adPath, useSsl);
    }


    /// <summary>
    /// read the AD users and groups?
    /// </summary>
    /// <param name="parameters">the crawler's parameters detailing how to connect and what to read</param>
    /// <returns>the users and groups found in AD</returns>
    private (Dictionary<string, LdapUser> users, Dictionary<string, LdapGroup> groups) SetupAdUsersAndGroups(
        CrawlerParameterSet parameters)
    {
        var adUser = new Dictionary<string, LdapUser>();
        var adGroups = new Dictionary<string, LdapGroup>();

        // Check if the user wants to use AD (optional)
        if (parameters.UseAd)
        {
            _logger.Info($"{_name}: Connecting to active directories...");
            try
            {
                var reader = new LdapReader(parameters.AdPath, parameters.UseSsl, parameters.Username, parameters.Password);
                foreach (var group in reader.GetAllGroups())
                {
                    adGroups[group.Identity] = group;
                }
                foreach (var group in CreateDomainGroups())
                {
                    adGroups[group.Identity] = group;
                }

                foreach (var user in reader.GetAllUsers())
                {
                    adUser[user.Identity] = user;
                }
            }
            catch
            {
                _logger.Error(
                    $@"{_name}: cannot connect to Active Directory
                 (username={parameters.Username},secure={parameters.UseSsl},adPath={parameters.AdPath})"
                );
                throw; // Re-throw to indicate a critical setup failure
            }
        }

        return (adUser, adGroups);
    }

    public string GetCurrentRefreshToken() => _propertyMap.TryGetValue(Source.REFRESH_TOKEN, out var tokenObj)
        ? tokenObj.ToString() ?? ""
        : "";

    /*********************************************-HELPER FUNCTIONS-*********************************************/

    /// <summary>
    /// Recursively crawls a directory, processing files and subdirectories.
    /// </summary>
    /// <param name="currentDirectory">The current directory path to crawl.</param>
    /// <param name="recursionDepth">starts at 0, how deep we are in the directory structure</param>
    private bool CrawlDirectory(string currentDirectory, int recursionDepth)
    {
        if (recursionDepth > 100)
            return true; // prevent infinite recursion
        
        try
        {
            // Process files in the current directory
            foreach (var filePath in Directory.GetFiles(currentDirectory))
            {
                if (!ProcessFile(filePath))
                    return false;
                if (!Active)
                    return false;
            }

            // Recursively process subdirectories
            foreach (var subDirectory in Directory.GetDirectories(currentDirectory))
            {
                if (!CrawlDirectory(subDirectory, recursionDepth + 1))
                    return false;
                if (!Active)
                    return false;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error($"{_name}: Access denied to directory {currentDirectory}: {ex.Message}");
            if (recursionDepth == 0)
                return false;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.Error($"{_name}: Directory not found: {currentDirectory}: {ex.Message}");
            if (recursionDepth == 0)
                return false;
        }
        catch
        {
            _logger.Error($"{_name}: An unexpected error occurred while crawling directory {currentDirectory}");
            return false;
        }

        return Active;
    }

    
    /// <summary>
    /// process a single file
    /// </summary>
    /// <param name="filePath">the path to the file to read</param>
    /// <returns>return false if we need to stop</returns>
    private bool ProcessFile(string filePath)
    {
        if (_api == null || _api.HasExceededCapacity())
            return true;
        
        var asset = new Asset();
        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            FileMetadata metadata = new FileMetadata
            {
                FilePath = filePath,
                FileSize = fileInfo.Length,
                LastWriteTime = fileInfo.LastWriteTime,
                CreatedTime = fileInfo.CreationTime
            };

            _logger.Debug($"{_name} Processing file: {filePath}");

            // Get ACL information
            metadata.AccessControlList = GetFileAccessControlInfo(filePath);

            // convert this Samba data into one of our assets
            asset = ConvertToAsset(metadata);
            if (_api != null)
            {
                if (_api.LastModifiedHasChanged(asset))
                {
                    asset.Filename = DownloadAssetData(asset, metadata);
                    return _api.ProcessAsset(asset);
                }
            }
        }
        catch (FileNotFoundException)
        {
            _logger.Error($"{_name}: File not found: {filePath}. It might have been moved or deleted.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error($"{_name}: Access denied to file {filePath}: {ex.Message}");
        }
        catch
        {
            _logger.Error($"{_name}: An unexpected error occurred while processing file {filePath}");
            return false;
        }
        finally
        {
            // remove temp file
            if (asset.Filename.Length > 0)
            {
                File.Delete(asset.Filename);
            }
        }

        return true;
    }
    

    /// <summary>
    /// Downloads a file from the share to the local download directory.
    /// </summary>
    /// <param name="sourceFilePath">The full path of the file on the share.</param>
    private string DownloadFile(string sourceFilePath)
    {
        try
        {
            var filename = FileUtils.GetTempFilename();
            File.Copy(sourceFilePath, filename, true); // 'true' to overwrite if this file already exists
            return filename;
        }
        catch (IOException ex)
        {
            _logger.Debug($"{_name}: Error downloading {sourceFilePath} (e.g., file in use, network issue): {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Debug($"{_name}: Permission denied to download {sourceFilePath}: {ex.Message}");
        }
        catch
        {
            _logger.Debug($"{_name}: An unexpected error occurred during download of {sourceFilePath}");
        }

        return "";
    }
    
    
    /// <summary>
    /// get all the required details of an SmbFile and put it inside a crawler document
    /// do not download the file's data yet, as we need to see if it has changed or not
    /// </summary>
    /// <param name="item">the file being processed</param>
    /// <returns>the crawler document containing all the required metadata</returns>
    private Asset ConvertToAsset(FileMetadata item)
    {
        var asset = new Asset
        {
            Url = item.FilePath
        };

        var fileExtension = Document.GetFileExtension(asset.Url);
        var mimetype = FileUtils.FileTypeToMimeType(fileExtension);
        asset.MimeType = mimetype;
        asset.Acls.AddRange(ConvertAcls(item));

        asset.Metadata[Document.META_LAST_MODIFIED_DATE_TIME] = item.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", _formatter);
        asset.LastModified = ToUnixEpochMilliseconds(item.LastWriteTime);
        asset.Metadata[Document.META_CREATED_DATE_TIME] = item.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss", _formatter);
        asset.Created = ToUnixEpochMilliseconds(item.CreatedTime);
        asset.BinarySize = item.FileSize;

        return asset;
    }


    /// <summary>
    /// Downloads asset data for the provided file metadata if specific conditions are met.
    /// and return the local temporary filename for the data (or empty string if we don't download it)
    /// </summary>
    /// <param name="item">The metadata of the file to be downloaded.</param>
    /// <returns>A string representing the downloaded file path, or an empty string if the file is not downloaded.</returns>
    private string DownloadAssetData(Asset asset, FileMetadata item)
    {
        if (item.FileSize > 0L)
        {
            // only download the file if we need to
            if (_api != null && !_api.IsInventoryOnly(asset))
            {
                // Download the file
                return DownloadFile(item.FilePath);
            }
        }

        return "";
    }


    /// <summary>
    /// Converts a DateTime to Unix epoch (seconds since 1970-01-01 00:00:00 UTC).
    /// </summary>
    /// <param name="dateTime">The DateTime object to convert. It's highly recommended to use UTC DateTime.</param>
    /// <returns>A long representing the number of seconds since the Unix epoch.</returns>
    private long ToUnixEpochMilliseconds(DateTime dateTime)
    {
        // Ensure the DateTime is in UTC before calculating the difference.
        // If it's not UTC, convert it. If it's Unspecified, treat it as UTC for this conversion.
        var utcDateTime = dateTime.Kind == DateTimeKind.Unspecified ?
                                 DateTime.SpecifyKind(dateTime, DateTimeKind.Utc) :
                                 dateTime.ToUniversalTime();
        var diff = utcDateTime - _epoch;
        return (long)diff.TotalSeconds * 1000L;
    }

    /// <summary>
    /// Retrieves access control information for a given file.
    /// </summary>
    /// <param name="filePath">The full path to the file.</param>
    /// <returns>A list of AccessControlEntry objects.</returns>
    private List<AccessControlEntry> GetFileAccessControlInfo(string filePath)
    {
        List<AccessControlEntry> aclEntries = new List<AccessControlEntry>();
        try
        {
            var fileInfo = new FileInfo(filePath);
            var fileSecurity = fileInfo.GetAccessControl();

            // Get access rules, including inherited ones, for SecurityIdentifier objects
            foreach (FileSystemAccessRule rule in fileSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                AccessControlEntry ace = new AccessControlEntry
                {
                    FileSystemRights = rule.FileSystemRights.ToString(),
                    AccessControlType = rule.AccessControlType.ToString(),
                    IsInherited = rule.IsInherited
                };

                // Attempt to translate the SecurityIdentifier (SID) to an NTAccount (Domain\User or Domain\Group)
                try
                {
                    NTAccount ntAccount = (NTAccount)rule.IdentityReference.Translate(typeof(NTAccount));
                    SecurityIdentifier sid = (SecurityIdentifier)rule.IdentityReference;
                    ace.Identity = ntAccount.Value;

                    // Basic heuristic to determine if it's likely a user or group.
                    // A definitive determination requires querying Active Directory for the objectClass.
                    if (sid.IsWellKnown(WellKnownSidType.BuiltinUsersSid) ||
                        sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
                        sid.IsWellKnown(WellKnownSidType.BuiltinGuestsSid) ||
                        sid.IsWellKnown(WellKnownSidType.BuiltinPowerUsersSid) ||
                        sid.IsWellKnown(WellKnownSidType.NetworkServiceSid) ||
                        sid.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
                        sid.IsWellKnown(WellKnownSidType.AuthenticatedUserSid) ||
                        sid.IsWellKnown(WellKnownSidType.CreatorOwnerSid) ||
                        sid.IsWellKnown(WellKnownSidType.WorldSid) ||
                        sid.IsWellKnown(WellKnownSidType.LocalServiceSid) ||
                        sid.IsWellKnown(WellKnownSidType.NetworkSid))
                    {
                        ace.Type = "Well-Known";
                    }
                    else if (ace.Identity.EndsWith("$")) // Common for machine accounts
                    {
                        ace.Type = "Machine";
                    }
                    else if (ace.Identity.Contains("\\"))
                    {
                        // If it contains a domain, it's likely a domain user or group.
                        // Without AD lookup, we can't definitively say user vs group.
                        // We'll mark it as needing AD lookup for full details.
                        ace.Type = "Domain";
                    }
                    else
                    {
                        ace.Type = "Local";
                    }

                    aclEntries.Add(ace);

                }
                catch (IdentityNotMappedException)
                {
                    // This occurs if the SID cannot be translated to an NTAccount (e.g., orphaned SID)
                    ace.Identity = rule.IdentityReference.Value; // Use the raw SID value
                    ace.Type = "Unresolved SID";
                }
                catch
                {
                    // Catch any other errors during SID translation
                    _logger.Warn($"Error translating SID {rule.IdentityReference.Value}");
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            _logger.Error($"  Access denied to read ACL for {filePath}. You may need higher permissions.");
        }
        catch
        {
            _logger.Error($"  Error getting ACL for {filePath}");
        }
        return aclEntries;
    }


    /// <summary>
    /// convert the security principals for a file
    /// </summary>
    /// <param name="item">the item to get ACLs for</param>
    /// <returns>a set of ACLs</returns>
    private List<AssetAcl> ConvertAcls(FileMetadata item)
    {
        var assetAclList = new List<AssetAcl>();
        item.AccessControlList.ForEach(ace =>
        {
            if (ace.Type == "Well-Known" && _adGroups.ContainsKey(ace.Identity.ToLower()))
            {
                // these shall pass
            }
            else if (ace.Type != "Domain" && ace.Type != "Local")
            {
                return; // Continue to the next ACE
            }
            if (ace.AccessControlType != "Allow")
            {
                return; // not allowed to access
            }

            const bool write = false;
            const bool delete = false;

            AssetAcl? acl = null;
            if (_adUsers.TryGetValue(ace.Identity.Trim().ToLowerInvariant(), out var user))
            {
                var email = user.Email;
                var samAccount = user.SamAccountName ?? "";
                if (samAccount.Length > 1)
                {
                    samAccount = char.ToUpper(samAccount[0]) + samAccount.Substring(1).ToLower();
                } else {
                    samAccount = samAccount.ToUpper();
                }
                var displayName = (user.DisplayName == "") ? samAccount : user.DisplayName;
                if (!string.IsNullOrEmpty(email))
                {
                    acl = new AssetAcl(
                        name: email,
                        displayName,
                        AssetAcl.CreateAccessString(read: true, write: write, delete: delete)
                    );
                }
            }
            else if (_adGroups.TryGetValue(ace.Identity.Trim().ToLowerInvariant(), out var group))
            {
                var displayName = (group.DisplayName == "") ? group.SamAccountName : group.DisplayName;
                acl = new AssetAcl(
                    displayName,
                    AssetAcl.CreateAccessString(read: true, write: write, delete: delete),
                    group.Members
                );
            }

            if (acl != null)
            {
                assetAclList.Add(acl);
            }
        });
        return assetAclList;
    }

    /// <summary>
    /// helper: create a list of domain groups to use for standard AD group mapping
    /// </summary>
    /// <returns>a list of LdapGroups that are domain well known groups</returns>
    private List<LdapGroup> CreateDomainGroups() 
    {
        var domainGroupList = new List<LdapGroup>
        {
            new()
            {
                DistinguishedName = "Users",
                SamAccountName = "Users",
                DisplayName = "Users",
                Identity = "builtin\\users"
            },
            new()
            {
                DistinguishedName = "Administrators",
                SamAccountName = "Administrators",
                DisplayName = "Administrators",
                Identity = "builtin\\administrators"
            }
        };
        return domainGroupList;
    }

    public string StateToJson() => ""; // Not implemented, returning empty string as per Kotlin
    public void StateFromJson(string json) { } // Not implemented
    public void ClearState() { } // Not implemented
}
