namespace Crawlers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Diagnostics;
using NUnit.Framework;

/// <summary>
/// the main communications bus with the external SimSage system to transfer files and data
/// called and used by the crawlers
/// </summary>
public class PlatformCrawlerCommonProxy : ICrawlerApi, IExternalSourceLogger
{
    private static readonly bool Verbose = Vars.Get("external_crawler_verbose").ToLowerInvariant() == "true";
    private static readonly JsonSerializer Mapper = JsonSerializer.Create();
    private static readonly RockLogger Logger = RockLogger.GetLogger(typeof(PlatformCrawlerCommonProxy));

    private const string NotLoaded = "source not loaded (null)";
    private const string Base64Prefix = ";base64,";
    private const string lastModifiedPrefix = "last-modified-";
    private readonly bool _isWindows = RockUtils.IsWindows();
    private const int CacheLifespanInDays = 365;
    private const int MaxUploadBlockSize = 1024 * 1024 * 10; // 10MB
    // if SimSage isn't reacable, wait this many seconds
    private const int WaitForNetworkErrorTimeoutInSeconds = 60;
    public bool Active { get; set; } = true;

    private readonly string _simSageEndpoint;
    private readonly string _simSageApiVersion;
    private readonly string _organisationId;
    private readonly string _kbId;
    private readonly string _sid;
    private static string? _aes;
    private readonly int _sourceId;
    private readonly bool _useEncryption;
    private readonly bool _exitAfterFinishing;
    private readonly bool _allowSelfSignedCertificate;
    private readonly string _crawlerType;

    // for generating random numbers for picking shared keys
    private readonly Random _rng = new();

    // how many files we've uploaded thus far
    private int _numFilesUploaded;
    private long _numFilesSeen;
    private int _numErrors;

    // is the crawler running?
    private bool _running;

    // access to the crawler
    private Source? _source;
    private long _sourceNextRefreshTime;
    private const long SourceRefreshInterval = 120_000L; // 2L * 60_000L // every 2 minutes
    
    // crawler cache
    private readonly SqliteAssetDao? _cacheDao;

    // the run id
    private long _runId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Represents a proxy for a platform crawler, providing integration with SimSage services
    /// and offering functionality for logging external sources.
    /// Implements both ICrawlerApi and IExternalSourceLogger interfaces.
    /// </summary>
    /// <remarks>
    /// This class interfaces with SimSage endpoints, initializes system configurations, and optionally
    /// enables caching for assets using a SQLite database. Required parameters ensure proper setup, while
    /// optional settings allow customization such as encryption, certificate validation, and caching.
    /// </remarks>
    public PlatformCrawlerCommonProxy(
        string serviceName,
        string simSageEndpoint,
        string simSageApiVersion,
        string crawlerType,
        string organisationId,
        string kbId,
        string sid,
        string aes,
        int sourceId,
        bool useEncryption,
        bool exitAfterFinishing,
        bool allowSelfSignedCertificate,
        bool useCache
    )
    {
        _simSageEndpoint = simSageEndpoint;
        _simSageApiVersion = simSageApiVersion;
        _organisationId = organisationId;
        _kbId = kbId;
        _sid = sid;
        _aes = aes;
        if (_aes == null)
            throw new ArgumentException("AES value cannot be null");
        _sourceId = sourceId;
        _useEncryption = useEncryption;
        _exitAfterFinishing = exitAfterFinishing;
        _allowSelfSignedCertificate = allowSelfSignedCertificate;
        _crawlerType = crawlerType;

        // set up mime-types in the system
        FileUtils.ReadMimeTypeInformation();

        if (!useCache) return;
        _cacheDao = new SqliteAssetDao(serviceName);
        _cacheDao.Initialize();
#pragma warning disable CA1416
        EventLog.WriteEntry(serviceName, $"Using crawler_cache.db: {_cacheDao.CacheDatabasePath}", EventLogEntryType.Information);
#pragma warning restore CA1416
    }

    /// <summary>
    /// Sign in a user to SimSage and return the session-id for the connection
    /// </summary>
    public Source GetSource()
    {
        _source ??= GetCrawlerFromDb();
        return _source ?? throw new ArgumentException(NotLoaded);
    }

    /// <summary>
    /// For unit testing - set the source
    /// </summary>
    public void SetSource(Source source)
    {
        _source = source;
    }

    /// <summary>
    /// Retrieves the crawler details from the database and updates the source object.
    /// Also refreshes the source check interval and configures the external source logger if needed.
    /// </summary>
    /// <returns>The retrieved source object.</returns>
    /// <exception cref="ArgumentException">Thrown if the mapped crawler JSON is null.</exception>
    private Source GetCrawlerFromDb()
    {
        Logger.Debug("getCrawlerFromDb()");
        var seed = _rng.Next();
        var url = !_useEncryption
            ? $"{_simSageEndpoint}/crawler/external/crawler"
            : $"{_simSageEndpoint}/crawler/external/secure/{seed}";

        var jsonStr = HttpPost(
            url,
            [
                "objectType", "CMExternalCrawler",
                "organisationId", _organisationId,
                "kbId", _kbId,
                "sid", _sid,
                "sourceId", _sourceId
            ],
            _useEncryption,
            seed,
            _simSageApiVersion,
            _allowSelfSignedCertificate
        );
        var data = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
        CheckError(data);

        // update the interval for checking the source
        _sourceNextRefreshTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + SourceRefreshInterval;

        _source = Mapper.ReadValue<Source>(jsonStr);

        // set up the external source logger according to transmitExternalLogs
        if (_source != null)
        {
            if (_source.CrawlerType != _crawlerType)
            {
                throw new ArgumentException($"Source type incorrect, expected \"{_crawlerType}\" but got \"{_source.CrawlerType}\"");
            }
            RockLogger.SetUpExternalSourceLogger(_source.TransmitExternalLogs ? this : null);
        }

        return _source ?? throw new ArgumentException("crawler json-mapped to null");
    }

    /// <summary>
    /// Has the crawler exceeded the maximum files to read capacity?
    /// </summary>
    public bool HasExceededCapacity()
    {
        var sourceMaxItems = _source?.MaxItems ?? 0;
        if (sourceMaxItems > 0 && _numFilesUploaded >= sourceMaxItems)
        {
            Logger.Debug($"crawler \"{_source?.Name ?? ""}\" has exceeded maximum-capacity of {sourceMaxItems}, stopping crawl");
            return true;
        }
        return false;
    }

    // is this an inventory-only asset?
    public bool IsInventoryOnly(Asset asset)
    {
        var maxBinarySize = FileUtils.MaximumSizeInBytesForMimeType(asset.MimeType);
        // too big
        if (asset.BinarySize > maxBinarySize) return true;
        if (asset.BinarySize <= 0) return true;
        if (!FileUtils.IsValidMimeType(asset.MimeType)) return true;
        if (_source == null)
            throw new ArgumentException("source is null");
        return _source.IsInventoryOnly(asset.MimeType);
    }

    /// <summary>
    /// check propertyMap contains propertyNameList values
    /// </summary>
    /// <param name="name">name of the crawler</param>
    /// <param name="propertyMap">the properties of the crawler</param>
    /// <param name="propertyNameList">the names of the properties</param>
    public void VerifyParameters(string name, Dictionary<string, object> propertyMap, List<string> propertyNameList)
    {
        foreach (var propertyName in propertyNameList)
        {
            if (!propertyMap.ContainsKey(propertyName))
            {
                throw new ArgumentException($"missing property {propertyName}");
            }
        }
    }

    /// <summary>
    /// send a log entry to the SimSage platform
    /// </summary>
    /// <param name="logEntry">the log entry to send</param>
    public void TransmitLogEntryToPlatform(string logEntry)
    {
        var seed = _rng.Next();
        var url = !_useEncryption
            ? $"{_simSageEndpoint}/crawler/external/crawler/log"
            : $"{_simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            HttpPost(
                url, [
                    "objectType", "CMExternalLogEntry",
                    "organisationId", _organisationId,
                    "kbId", _kbId,
                    "sid", _sid,
                    "sourceId", _sourceId,
                    "logEntry", logEntry
                ], _useEncryption, seed, _simSageApiVersion, _allowSelfSignedCertificate
            );
        }
        catch (Exception ex)
        {
            // this must be a console.WriteLine - otherwise it'll repeat the error
            Console.WriteLine($"transmitLogEntryToPlatform(): {ex.Message}");
        }
    }

    /// <summary>
    /// Slow down if requested
    /// </summary>
    private void AdjustCrawlRate()
    {
        var s = _source;
        if (s is { FilesPerSecond: > 1.0f })
        {
            Thread.Sleep((int)s.FilesPerSecond);
        }
    }

    /// <summary>
    /// record an asset exception
    /// </summary>
    /// <param name="url">the url of the asset/document</param>
    /// <param name="exception">the exception to record (a string)</param>
    /// <param name="webUrl">the web URL (a string)</param>
    /// <param name="folderUrl">the folder this item is located in (if applicable)</param>
    /// <param name="deltaRootId">the delta for this folder (if applicable)</param>
    public void RecordExceptionAsset(
        string url,
        string exception,
        string webUrl,
        string folderUrl,
        string deltaRootId
    )
    {
        // check parameters before posting
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(_kbId) ||
            string.IsNullOrWhiteSpace(_organisationId) || _sourceId <= 0 || string.IsNullOrWhiteSpace(_sid) || _runId == 0L
        )
        {
            throw new ArgumentException("invalid parameter(s)");
        }

        _numFilesSeen += 1;
        Logger.Debug($"recordExceptionAsset(url={url},exception={exception},webUrl={webUrl})");
        var seed = _rng.Next();
        var postUrl = !_useEncryption
            ? $"{_simSageEndpoint}/crawler/external/document/recordfailure"
            : $"{_simSageEndpoint}/crawler/external/secure/{seed}";
        try
        {
            var jsonStr = HttpPost(
                postUrl, [
                    "objectType", "CMFailedSourceDocument",
                    "organisationId", _organisationId,
                    "kbId", _kbId,
                    "sid", _sid,
                    "sourceId", _sourceId,
                    "sourceSystemId", url,
                    "webUrl", webUrl,
                    "deltaRootId", deltaRootId,
                    "runId", _runId,
                    "errorMessage", exception
                ], _useEncryption, seed, _simSageApiVersion, _allowSelfSignedCertificate
            );
            var data = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
            CheckError(data);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"recordExceptionAsset({url}): {ex.Message}", ex);
        }
    }


    /// <summary>
    /// Periodically update the source
    /// </summary>
    private void CheckCrawler()
    {
        if (_sourceNextRefreshTime < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            try
            {
                GetCrawlerFromDb();
            }
            catch (Exception ex)
            {
                Logger.Error($"CheckCrawler({ex.Message})", ex);
            }
        }
    }

    /// <summary>
    /// set a delta token on a source
    /// </summary>
    public void SetDeltaState(string deltaIndicator)
    {
        Logger.Debug($"setDeltaState({deltaIndicator})");
        var seed = _rng.Next();
        var url = !_useEncryption
            ? $"{_simSageEndpoint}/crawler/external/crawler/delta-token"
            : $"{_simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            var jsonStr = HttpPost(
                url,
                [
                    "objectType", "CMExternalCrawlerSetDeltaToken",
                    "organisationId", _organisationId,
                    "kbId", _kbId, "sid", _sid, "sourceId", _sourceId,
                    "deltaToken", deltaIndicator
                ],
                _useEncryption, seed, _simSageApiVersion, _allowSelfSignedCertificate
            );
            var data = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
            if (_source != null)
                _source.DeltaIndicator = deltaIndicator;
            CheckError(data);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"setDeltaState({deltaIndicator}): {ex.Message}");
        }
    }

    public string GetDeltaState() => _source?.DeltaIndicator ?? "";

    
    /// <summary>
    /// Determines if the "Last Modified" timestamp of the given asset has changed by comparing it with a cached value.
    /// Updates the cache with the new timestamp if changed.
    /// </summary>
    /// <param name="asset">The asset whose "Last Modified" timestamp is to be checked.</param>
    /// <returns>
    /// Returns true if the "Last Modified" timestamp of the asset has changed or the cache is disabled, otherwise false.
    /// </returns>
    public bool LastModifiedHasChanged(Asset asset)
    {
        // has this asset already been sent?
        if (_cacheDao != null)
        {
            var cachedHash = _cacheDao.Get(lastModifiedPrefix + asset.Url);
            if (cachedHash == "")
            {
                return true; // DNE, write it as having changed
            }
            if (cachedHash == asset.LastModified.ToString())
            {
                MarkFileAsSeen(asset); // hasn't changed, just mark it as seen
                return false;
            }
            // set the item's data in the cache, it has changed
            _cacheDao.Set(
                lastModifiedPrefix + asset.Url, 
                asset.LastModified.ToString(), 
                CacheLifespanInDays * 3600_000L * 24L
                );
        }

        return true;
    }
    

    /// <summary>
    /// Processes the given asset by performing validation, encoding, and uploading it to the system.
    /// </summary>
    /// <param name="externalAsset">The asset to be processed, containing metadata such as URL and content details.</param>
    /// <returns>True if all is good and we should continue processing assets, False if the crawler needs to abort.</returns>
    public bool ProcessAsset(Asset externalAsset)
    {
        // upload - valid or invalid (i.e. data/mime-type or no mime-type - all valid
        _numFilesUploaded += 1;

        try {
            CheckCrawler();

            Thread.Sleep(2000);

            // check we have the minimum requirements that the asset is valid
            if (externalAsset.Url.Trim().Length == 0)
            {
                Logger.Error($"processAsset: asset url is empty, ignoring {externalAsset.Url}");
                return true;
            }

            // wait for the crawler to be ready
            if (!WaitUntilCrawlerReady())
                return false;

            // have we added too many files for the given source?
            if (HasExceededCapacity())
                return true;

            // rate-limit this crawler
            AdjustCrawlRate();

            // has this asset already been sent?
            if (_cacheDao != null)
            {
                var assetHash = externalAsset.CalculateHash();
                var cachedHash = _cacheDao.Get(externalAsset.Url);
                if (cachedHash == assetHash)
                {
                    // the item is in the cache and hasn't changed - just mark it as processed
                    MarkFileAsSeen(externalAsset);
                    return true;
                }
                // set the item's data in cache for last modified and for content hash
                _cacheDao.Set(lastModifiedPrefix + externalAsset.Url, externalAsset.LastModified.ToString(), CacheLifespanInDays * 3600_000L * 24L);
                _cacheDao.Set(externalAsset.Url, assetHash, CacheLifespanInDays * 3600_000L * 24L);
            }

            // is this an inventory-only asset?  don't send the bytes
            if (IsInventoryOnly(externalAsset))
            {
                externalAsset.RemoveAssetTempFile();
            }

            var seed = (int)_rng.NextInt64();

            // upload this file
            _numFilesSeen += 1;

            FileUploadPost(
                _organisationId, _kbId, _sid, _sourceId,
                UploadExternalDocumentCmd.Convert(externalAsset), 
                externalAsset.Filename,
                _useEncryption, _runId, seed, _simSageEndpoint, _simSageApiVersion,
                FileUtils.MaximumSizeInBytesForMimeType(externalAsset.MimeType),
                _allowSelfSignedCertificate
            );

        } catch (Exception ex) {
            Logger.Error($"processAsset({externalAsset.Url}): {ex.Message}", ex);
            _numErrors += 1;
        }
        return true;
    }

    /// <summary>
    /// Marks a file as seen in the system, indicating that it has been processed.
    /// </summary>
    /// <param name="asset">The asset representing the file to be marked as seen. It contains the file's URL and other metadata.</param>
    /// <exception cref="ArgumentException">Thrown when there is an issue with the arguments provided for the marking process.</exception>
    public void MarkFileAsSeen(Asset asset)
    {
        CheckCrawler();
        asset.Filename = ""; // files already seen do not send data
        Logger.Debug($"markFileAsSeen(url={asset.Url})");
        var seed = _rng.Next();
        var postUrl = !_useEncryption
            ? $"{_simSageEndpoint}/crawler/external/crawler/mark-file-as-seen"
            : $"{_simSageEndpoint}/crawler/external/secure/{seed}";
        try
        {
            var jsonStr = HttpPost(
                postUrl, [
                    "objectType", "CMExternalCrawlerMarkFileAsSeen",
                    "organisationId", _organisationId,
                    "kbId", _kbId, "sid", _sid, "sourceId", _sourceId,
                    "runId", _runId,
                    "asset", asset
                ],
                _useEncryption, seed, _simSageApiVersion, _allowSelfSignedCertificate
            );
            var data = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
            CheckError(data);
            _numFilesSeen += 1;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"MarkFileAsSeen(): {ex.Message}");
        }
    }

    public bool Delete(string url)
    {
        Logger.Debug($"delete({url})");
        var seed = _rng.Next();
        var postUrl = !_useEncryption
            ? $"{_simSageEndpoint}/crawler/external/crawler/delete-url"
            : $"{_simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            var jsonStr = HttpPost(
                postUrl, [
                    "objectType", "CMExternalCrawlerDeleteUrl",
                    "organisationId", _organisationId,
                    "kbId", _kbId, "sid", _sid, "sourceId", _sourceId,
                    "url", url
                ],
                _useEncryption, seed, _simSageApiVersion, _allowSelfSignedCertificate
            );
            var data = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
            CheckError(data);
            return true;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"delete({url}): {ex.Message}");
        }
        return false;
    }

    public void DeleteFolder(string folderUrl)
    {
        Logger.Debug($"deleteFolder({folderUrl})");
        var seed = _rng.Next();
        var postUrl = !_useEncryption
            ? $"{_simSageEndpoint}/crawler/external/crawler/delete-folder"
            : $"{_simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            var jsonStr = HttpPost(
                postUrl, [
                    "objectType", "CMExternalCrawlerDeleteFolder",
                    "organisationId", _organisationId,
                    "kbId", _kbId, "sid", _sid, "sourceId", _sourceId,
                    "folderUrl", folderUrl
                ],
                _useEncryption, seed, _simSageApiVersion, _allowSelfSignedCertificate
            );
            var data = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
            CheckError(data);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"deleteFolder({folderUrl}): {ex.Message}");
        }
    }

    public void RenameFolders(List<RenameFolderAsset> changedFolders)
    {
        Logger.Debug($"renameFolders(numFolders={changedFolders.Count})");
        foreach (var folder in changedFolders)
        {
            var seed = _rng.Next();
            var postUrl = !_useEncryption
                ? $"{_simSageEndpoint}/crawler/external/crawler/rename-folder"
                : $"{_simSageEndpoint}/crawler/external/secure/{seed}";

            try
            {
                var jsonStr = HttpPost(
                    postUrl, [
                        "objectType", "CMExternalCrawlerRenameFolder",
                        "organisationId", _organisationId,
                        "kbId", _kbId, "sid", _sid, "sourceId", _sourceId,
                        "oldFolderNameUrl", folder.OriginalFolderName,
                        "newFolderNameUrl", folder.NewFolderName,
                        "acls", folder.AssetAclList
                    ],
                    _useEncryption, seed, _simSageApiVersion, _allowSelfSignedCertificate
                );
                var data = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
                CheckError(data);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"renameFolders(): {ex.Message}");
            }
        }
    }

    /// <summary>
    /// This is a proxy platform crawler - it is _never_ ready for platform crawling
    /// </summary>
    public bool IsActive() => false;

    /// <summary>
    /// Mark a file as seen - requires asset with full data if present
    /// in order to process archives on the other side of the fence
    /// </summary>
    /// <param name="asset">the asset to mark as seen with full data - this is required for archive files</param>
    /// <param name="incrementCounters">if true, the counters will be updated</param>
    /// <param name="runIdIn">an optional runId parameter to forgo calling the stats system (if > 0)</param>
    public void MarkFileAsSeen(Asset asset, bool incrementCounters, long runIdIn)
    {
        CheckCrawler(); // also check any source changes just in case
        Logger.Debug($"markFileAsSeen(url={asset.Url})");
        var seed = _rng.Next();
        var postUrl = !_useEncryption
            ? $"{_simSageEndpoint}/crawler/external/crawler/mark-file-as-seen"
            : $"{_simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            var jsonStr = HttpPost(
                postUrl, [
                    "objectType", "CMExternalCrawlerMarkFileAsSeen",
                    "organisationId", _organisationId,
                    "kbId", _kbId, "sid", _sid, "sourceId", _sourceId,
                    "runId", _runId,
                    "asset", asset
                ],
                _useEncryption, seed, _simSageApiVersion, _allowSelfSignedCertificate
            );
            var data = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
            CheckError(data);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"markFileAsSeen({asset.Url}): {ex.Message}");
        }
    }

    /// <summary>
    /// Start the file crawler running
    /// </summary>
    public void CrawlerStart(ICrawler platformCrawler)
    {
        if (_source?.IsExternal == false)
        {
            _running = false;
            return;
        }
        
        // cleanup cache
        _cacheDao?.CleanupExpiredEntries();

        var crawler = _source ?? throw new ArgumentException(NotLoaded);

        // check this crawler is a valid external crawler
        if (!crawler.IsExternal)
            throw new ArgumentException($"{crawler} is not set up as an external crawler");

        // set up a run-id
        _runId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Logger.Info($"{crawler}, starting a new run for {_runId}");

        // also signal the other pipe-lines that the crawler has started
        SignalCrawlerStart();

        // start crawling
        _numFilesSeen = 0;
        _numFilesUploaded = 0;
        _numErrors = 0;
        _running = true;
    }


    /// <summary>
    /// Is the system ready for the initial start
    /// This function will _wait_ until we're ready to start
    /// </summary>
    /// <returns><c>true</c> after we're ready to go, false if the crawler needs to stop / exit</returns>
    public bool WaitForStart()
    {
        if (_source == null) return false;

        var currentSchedule = _source?.Schedule ?? "";
        var currentScheduleEnabled = _source?.ScheduleEnable ?? false;
        var waitTimeInHours = CrawlerUtils.CrawlerWaitTimeInHours(
            CrawlerUtils.GetCurrentTimeIndicatorString(),
            _source?.Schedule ?? "",
            !_running
        );
        var waitTimeInMilliseconds = (waitTimeInHours * 3600_000L);
        if (waitTimeInMilliseconds > 0)
        {
            var prevTime = RockUtils.MilliSecondsDeltaToString(waitTimeInMilliseconds);
            var waitTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + waitTimeInMilliseconds;
            Logger.Info($"{_source?.Name}: waiting {prevTime} as per schedule");
            while (Active && waitTime > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            {
                // wait 10 seconds before checking the source again
                for (var i = 0; i < 10 && Active; i++)
                {
                    Thread.Sleep(1_000); // wait 10 seconds before checking again
                }
                if (!Active) break;
                // get any source changes
                CheckCrawler();
                if (currentSchedule != (_source?.Schedule ?? ""))
                    return false; // terminate crawler: schedule changed
                if (currentScheduleEnabled != (_source?.ScheduleEnable ?? false))
                    return false; // terminate crawler: schedule enabled status changed
            }
        }
        return Active;
    }


    /// <summary>
    /// Is the system ready for a crawl?
    /// This function will _wait_ until we're ready
    /// </summary>
    /// <returns><c>true</c> after we're ready to go, false if the crawler needs to stop / exit</returns>
    private bool WaitUntilCrawlerReady()
    {
        var currentSchedule = _source?.Schedule ?? "";
        var waitTimeInHours = CrawlerUtils.CrawlerWaitTimeInHours(
            CrawlerUtils.GetCurrentTimeIndicatorString(),
            currentSchedule,
            !_running
        );
        var waitTimeInMilliseconds = (waitTimeInHours * 3600_000L);
        var waitTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + waitTimeInMilliseconds;
        if (waitTimeInHours <= 0) return _running;
        var prevTime = RockUtils.MilliSecondsDeltaToString(waitTimeInMilliseconds); // pretty print
        Logger.Info($"{_source?.Name}: waiting {prevTime} as per schedule");
        while (Active && waitTime > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() && _source is { IsExternal: true })
        {
            Thread.Sleep(10_000); // wait 10 seconds before checking again

            if (!Active) break;
            
            // get any source changes - if false - we need to re-evaluate our status
            CheckCrawler();
            
            // if the schedule changes, the crawler is no longer "finished"
            if (_source?.Schedule != currentSchedule)
            {
                currentSchedule = _source?.Schedule ?? "";
                if (!_running)
                    SignalCrawlerStart();
                _running = true;
            }
            // abort if this is no longer an external crawler
            if (_source?.IsExternal == false)
            {
                _running = false;
                return false;
            }
            // re-evaluate our wait time - just in case
            waitTimeInHours = CrawlerUtils.CrawlerWaitTimeInHours(
                CrawlerUtils.GetCurrentTimeIndicatorString(),
                _source?.Schedule ?? "",
                !_running
            );
            waitTimeInMilliseconds = (waitTimeInHours * 3600_000L);
            waitTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + waitTimeInMilliseconds;
            var nextTime = RockUtils.MilliSecondsDeltaToString(waitTimeInMilliseconds);
            if (prevTime != nextTime)
            {
                prevTime = nextTime;
                Logger.Info($"{_source?.Name}: waiting {nextTime} as per schedule");
            }
        }
        return _running;
    }

    public void CrawlerDone()
    {
        _running = false;

        if (_numFilesSeen > 0)
        {
            Logger.Info($"{_source?.Name}, finished runId {_runId}");
            // signal the crawler has finished to the pipe-line
            CrawlerFinished();
        }
        else
        {
            Logger.Warn($"crawler \"{_source?.Name}\" didn't get any files, has finished run {_runId}");
            // signal the crawler has finished to the pipe-line
            CrawlerFinished();
        }

        if (_exitAfterFinishing)
        {
            Logger.Info($"crawler \"{_source?.Name}\" exit after run finished (exit_after_crawl=true)");
            FileUtils.Shutdown(exitCode: 0);
        }

        WaitUntilCrawlerReady();
    }

    public void CrawlerCrashed(string reason)
    {
        _running = false;
        _numFilesSeen = 0;
        WaitUntilCrawlerReady();
    }

    /// <summary>
    /// Signal an external crawler is starting
    /// </summary>
    private void SignalCrawlerStart()
    {
        Logger.Debug("signalCrawlerStart()");
        var seed = _rng.Next();
        var url = !_useEncryption
            ? $"{_simSageEndpoint}/crawler/external/crawler/start"
            : $"{_simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            var jsonStr = HttpPost(
                url, [
                    "objectType", "CMExternalCrawlerStart", "organisationId", _organisationId,
                    "kbId", _kbId, "sid", _sid, "sourceId", _sourceId, "runId", _runId
                ], _useEncryption, seed, _simSageApiVersion, _allowSelfSignedCertificate
            );
            var data = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
            CheckError(data);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"signalCrawlerStart(): {ex.Message}");
        }
    }

    /// <summary>
    /// Signal a crawl has finished
    /// </summary>
    private void CrawlerFinished()
    {
        // finished
        Logger.Info($"{_source}, has finished run {_runId}");

        var seed = _rng.Next();
        var url = !_useEncryption
            ? $"{_simSageEndpoint}/crawler/external/crawler/finish"
            : $"{_simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            var jsonStr = HttpPost(
                url, [
                    "objectType", "CMExternalCrawlerStop", "organisationId", _organisationId,
                    "kbId", _kbId, "sid", _sid, "sourceId", _sourceId, "numErrors", _numErrors, "runId", _runId,
                    "numFilesSeen", _numFilesSeen
                ], _useEncryption, seed, _simSageApiVersion, _allowSelfSignedCertificate
            );
            var data = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
            CheckError(data);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"crawlerFinished(): {ex.Message}");
        }
    }


    /// <summary>
    /// Upload a file to SimSage (with or without payload)
    /// </summary>
    private static void FileUploadPost(
        string organisationId, string kbId, string sid, int sourceId,
        UploadExternalDocumentCmd file, string filename, bool useEncryption, long runId, int seed,
        string simSageEndpoint, string simSageApiVersion, 
        long maxSizeInBytes, bool allowSelfSignedCertificate
    )
    {
        var tempFile = !string.IsNullOrEmpty(filename) ? new FileInfo(filename) : null;
        var totalFileSize = tempFile?.Length ?? 0;
        var jobId = Guid.NewGuid().ToString(); 
        try
        {
            if (tempFile?.Exists == true && totalFileSize > 0 && totalFileSize < maxSizeInBytes)
            {
                // split the data to send into blocks
                var totalParts = (int)Math.Ceiling((double)totalFileSize / (double)MaxUploadBlockSize);
                var buffer = new byte[MaxUploadBlockSize];
                Logger.Debug($"fileUploadPost(url={file.Url},size={totalFileSize},blocks={totalParts},jobId={jobId})");
                using var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
                foreach (var partId in Enumerable.Range(0, totalParts))
                {
                    var dataSize = fileStream.Read(buffer, 0, buffer.Length);
                    PartialUpload(
                        organisationId, kbId, sid, sourceId,
                        partId, totalParts, buffer, dataSize, jobId, totalFileSize,
                        file, useEncryption, runId,
                        seed, simSageEndpoint, simSageApiVersion, allowSelfSignedCertificate
                    );
                }
            }
            else
            {
                // perform a partial upload without any data - because the data is bigger than allowed
                Logger.Debug($"fileUploadPost(url={file.Url},size={totalFileSize},data=null)");
                PartialUpload(
                    organisationId, kbId, sid, sourceId,
                    0, 1, null, 0, jobId, totalFileSize,
                    file, useEncryption, runId,
                    seed, simSageEndpoint, simSageApiVersion, allowSelfSignedCertificate
                );
            }
        }
        finally
        {
            tempFile?.Delete();
        }
    }


    /// <summary>
    /// helper - upload a file to SimSage, but in parts if necessary
    /// </summary>
    /// <param name="organisationId">the organisation</param>
    /// <param name="kbId">the kb</param>
    /// <param name="sid">the security id</param>
    /// <param name="sourceId">the sourceId</param>
    /// <param name="partId">the partId for the upload, starting at 0</param>
    /// <param name="totalParts">the total parts to expect > 0</param>
    /// <param name="data">the data to be send, can be null</param>
    /// <param name="dataSize">the size of the data to be sent [0,dataSize]</param>
    /// <param name="jobId">a random guid to identify this job</param>
    /// <param name="totalFileSize">the total size of the file</param>
    /// <param name="file">the file's details</param>
    /// <param name="useEncryption">true to use message encryption (AES 256)</param>
    /// <param name="runId">the run this file belongs to</param>
    /// <param name="seed">the random seed for data encryption</param>
    /// <param name="simSageEndpoint">the server to talk to</param>
    /// <param name="simSageApiVersion">the API version of SimSage</param>
    /// <param name="allowSelfSignedCertificate">lacks security for self signed certs</param>
    private static void PartialUpload(
        string organisationId, 
        string kbId, 
        string sid, 
        int sourceId,
        int partId, 
        int totalParts, 
        byte[]? data,
        int dataSize,
        string jobId,
        long totalFileSize,
        UploadExternalDocumentCmd file, 
        bool useEncryption, 
        long runId, 
        int seed,
        string simSageEndpoint, 
        string simSageApiVersion, 
        bool allowSelfSignedCertificate
        )
    {
        var url = !useEncryption
            ? $"{simSageEndpoint}/crawler/external/document/upload"
            : $"{simSageEndpoint}/crawler/external/secure/{seed}";
        var base64Data = "";
        // never post more data than we're supposed to
        if (data != null && dataSize is > 0 and <= MaxUploadBlockSize)
        {
            base64Data = Base64Prefix + Convert.ToBase64String(data[..dataSize]);
        }
        var jsonStr = HttpPost(
            url, [
                "objectType", "CMUploadDocument", "organisationId", organisationId, "kbId", kbId, "sid", sid,
                "sourceId", sourceId, "url", file.Url, "mimeType", file.MimeType, "runId", runId,
                "acls", AssetAcl.UniqueAcls(file.Acls), "title", file.Title, "author", file.Author,
                "changeHash", file.ChangeHash, "contentHash", file.ContentHash,
                "partId", partId, "totalParts", totalParts, "jobId", jobId, "totalFileSize", totalFileSize,
                "data", base64Data, "created", file.Created, "lastModified", file.LastModified,
                "size", file.Size, "metadata", file.Metadata, "categories", file.Categories, "puid", file.Puid,
                "template", file.Template, "binarySize", file.BinarySize
            ], useEncryption, seed, simSageApiVersion, allowSelfSignedCertificate
        );
        var data2 = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
        CheckError(data2);
    }

    
    /// <summary>
    /// POST JSON data and return json result
    /// </summary>
    /// <param name="url">the url to post to</param>
    /// <param name="nameValues">an array of name, value, ...</param>
    /// <param name="useEncryption">are we using encryption</param>
    /// <param name="seed">an integer seed for selecting a random value</param>
    /// <param name="simSageApiVersion">always 1 at present</param>
    /// <param name="allowSelfSignedCertificate">if True, certificate strict checking is disabled</param>
    private static string HttpPost(
        string url,
        object[] nameValues,
        bool useEncryption,
        int seed,
        string simSageApiVersion,
        bool allowSelfSignedCertificate
    )
    {
        string str = "";
        HttpResponseMessage? response = null;
        HttpClient? client = null;
        var objectType = "";
        var encryptionKey = "";
        var retry = false;
        do
        {
            retry = false; // assume we succeed
            try
            {
                client = RockUtils.NewHttpClient(allowSelfSignedCertificate);
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Access-Control-Request-Method", "POST");
                request.Headers.Add("API-Version", simSageApiVersion);

                var sendMap = new Dictionary<string, object>();
                for (var i = 0; i < nameValues.Length / 2; i++)
                {
                    var key = nameValues[i * 2].ToString();
                    if (key == null) continue;
                    var value = nameValues[i * 2 + 1];
                    sendMap[key] = value;
                }
                objectType = sendMap.TryGetValue("objectType", out var value1) ? value1.ToString() ?? "" : "";

                // encrypted?
                var payload = Mapper.WriteValueAsString(sendMap);
                if (useEncryption)
                {
                    encryptionKey = Sha512.GenerateSha512Hash(
                        _aes ?? "",
                        SharedSecrets.GetRandomGuid(_aes ?? "", seed).ToString()
                        );
                    var encryptedBody = AesEncryption.Encrypt(payload, encryptionKey);
                    if (Verbose)
                        Logger.Info($"POST encrypted body {encryptedBody.Length} bytes");
                    request.Content = new StringContent(encryptedBody, Encoding.UTF8, "application/json");
                }
                else
                {
                    if (Verbose)
                        Logger.Info($"POST body {payload.Length} bytes");
                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                }

                response = client.SendAsync(request).Result; // use async
                response.EnsureSuccessStatusCode(); // Throws if not success
                str = response.Content.ReadAsStringAsync().Result;
            }
            catch (HttpRequestException ex)
            {
                CheckReturnError(url, response, encryptionKey); // Check for specific error message
                throw new ArgumentException($"could not read POST contents of {url}, (cmd:{objectType}) exception: ({ex.Message})", ex);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(ex.Message) && ex.Message.Contains("No connection could be made"))
                {
                    // can't log - just write to console
                    Console.WriteLine($"SimSage is not reachable over the network, trying again in {WaitForNetworkErrorTimeoutInSeconds} seconds.");
                    retry = true;
                }
                else
                {
                    throw new ArgumentException($"could not read POST contents of {url}, (cmd:{objectType}) exception: ({ex.Message})", ex);
                }
            }
            finally
            {
                response?.Dispose();
                client?.Dispose();
            }

            // keep trying until the system comes back online
            if (retry)
            {
                Thread.Sleep(WaitForNetworkErrorTimeoutInSeconds * 1000);
            }

        } while (retry);

        if (!string.IsNullOrWhiteSpace(str) && useEncryption)
            {
                return AesEncryption.Decrypt(
                    str,
                    Sha512.GenerateSha512Hash(
                        _aes ?? "",
                        SharedSecrets.GetRandomGuid(_aes ?? "", seed).ToString()
                        )
                );
            }
        return str;
    }

    /// <summary>
    /// Check there is an actual error and throw an exception if there is
    /// </summary>
    private static void CheckError(Dictionary<string, object>? data)
    {
        if (data == null)
            throw new ArgumentException("no data returned (null)");

        if (data.TryGetValue("error", out var value))
        {
            var errStr = value as string;
            if (!string.IsNullOrEmpty(errStr))
            {
                throw new ArgumentException(errStr);
            }
        }
    }

    /// <summary>
    /// @return a special error code if we can find it in the return message
    /// </summary>
    private static void CheckReturnError(string url, HttpResponseMessage? response, string key)
    {
        if (response == null) return;
        if (response.StatusCode < System.Net.HttpStatusCode.OK || response.StatusCode > System.Net.HttpStatusCode.PartialContent)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new ArgumentException($"POST: Incorrect sessionId ({url})");
            }
            // do we have an error object on the return?
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "";
            var jsonStr = "";
            if (contentType.Contains("text/plain") && !string.IsNullOrEmpty(key))
            {
                var textStr = response.Content.ReadAsStringAsync().Result;
                jsonStr = AesEncryption.Decrypt(textStr, key);
            }
            else if (contentType.Contains("application/json"))
            {
                jsonStr = response.Content.ReadAsStringAsync().Result;
            }

            if (!string.IsNullOrEmpty(jsonStr) && jsonStr.StartsWith("{"))
            {
                var errorMap = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
                if (errorMap == null)
                {
                    throw new ArgumentException($"could not read POST contents of {url}, exception: data null ({jsonStr})");
                }
                if (errorMap.TryGetValue("error", out var value))
                {
                    var errStr = value as string;
                    if (!string.IsNullOrEmpty(errStr))
                    {
                        throw new ArgumentException($"{errStr}: POST: {(int)response.StatusCode}, {url}");
                    }
                }
                throw new ArgumentException($"could not read POST error: {(int)response.StatusCode}, contents of {url}, exception: data null ({jsonStr})");
            }
            throw new ArgumentException($"POST: error {(int)response.StatusCode}, {url}");
        }
    }

    string ICrawlerApi.GetDeltaState()
    {
        return _source?.DeltaIndicator ?? "";
    }

    
}
