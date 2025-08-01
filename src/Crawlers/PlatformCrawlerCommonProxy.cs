namespace Crawlers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Diagnostics;
using Newtonsoft.Json;

public class PlatformCrawlerCommonProxy : ICrawlerApi, IExternalSourceLogger
{
    private static readonly bool Verbose = Vars.Get("external_crawler_verbose").ToLowerInvariant() == "true";
    private static readonly JsonSerializer Mapper = JsonSerializer.Create();
    private static readonly RockLogger Logger = RockLogger.GetLogger(typeof(PlatformCrawlerCommonProxy));

    private const string NotLoaded = "source not loaded (null)";
    private const string Base64Prefix = ";base64,";
    private readonly bool isWindows = RockUtils.IsWindows();
    private readonly int cacheLifespanInDays = 30;
    public bool Active { get; set; } = true;

    private readonly string simSageEndpoint;
    private readonly string simSageApiVersion;
    private readonly string organisationId;
    private readonly string kbId;
    private readonly string sid;
    private static string? _aes;
    private readonly int sourceId;
    private readonly bool useEncryption;
    private readonly bool exitAfterFinishing;
    private readonly bool allowSelfSignedCertificate;
    private readonly string crawlerType;

    // for generating random numbers for picking shared keys
    private readonly Random rng = new Random();

    // how many files we've uploaded thus far
    private int numFilesUploaded;
    private long numFilesSeen;
    private int numErrors;

    // is the crawler running?
    private bool running;

    // access to the crawler
    private Source? source;
    private long sourceNextRefreshTime;
    private const long SourceRefreshInterval = 120_000L; // 2L * 60_000L // every 2 minutes
    
    // crawler cache
    private readonly SqliteAssetDao? _cacheDao;

    // the run id
    private long runId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

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
        this.simSageEndpoint = simSageEndpoint;
        this.simSageApiVersion = simSageApiVersion;
        this.organisationId = organisationId;
        this.kbId = kbId;
        this.sid = sid;
        _aes = aes;
        if (_aes == null)
            throw new ArgumentException("AES value cannot be null");
        this.sourceId = sourceId;
        this.useEncryption = useEncryption;
        this.exitAfterFinishing = exitAfterFinishing;
        this.allowSelfSignedCertificate = allowSelfSignedCertificate;
        this.crawlerType = crawlerType;

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
        source ??= GetCrawlerFromDb();
        return source ?? throw new ArgumentException(NotLoaded);
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
        var seed = rng.Next();
        var url = !useEncryption
            ? $"{simSageEndpoint}/crawler/external/crawler"
            : $"{simSageEndpoint}/crawler/external/secure/{seed}";

        var jsonStr = HttpPost(
            url,
            [
                "objectType", "CMExternalCrawler",
                "organisationId", organisationId,
                "kbId", kbId,
                "sid", sid,
                "sourceId", sourceId
            ],
            useEncryption,
            seed,
            simSageApiVersion,
            allowSelfSignedCertificate
        );
        var data = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
        CheckError(data);

        // update the interval for checking the source
        sourceNextRefreshTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + SourceRefreshInterval;

        source = Mapper.ReadValue<Source>(jsonStr);

        // set up the external source logger according to transmitExternalLogs
        if (source != null)
        {
            if (source.CrawlerType != crawlerType)
            {
                throw new ArgumentException($"Source type incorrect, expected \"{crawlerType}\" but got \"{source.CrawlerType}\"");
            }
            RockLogger.SetUpExternalSourceLogger(source.TransmitExternalLogs ? this : null);
        }

        return source ?? throw new ArgumentException("crawler json-mapped to null");
    }

    /// <summary>
    /// Has the crawler exceeded the maximum files to read capacity?
    /// </summary>
    public bool HasExceededCapacity()
    {
        var sourceMaxItems = source?.MaxItems ?? 0;
        if (sourceMaxItems > 0 && numFilesUploaded >= sourceMaxItems)
        {
            Logger.Debug($"crawler \"{source?.Name ?? ""}\" has exceeded maximum-capacity of {sourceMaxItems}, stopping crawl");
            return true;
        }
        return false;
    }

    // is this an inventory-only asset?
    public bool IsInventoryOnly(string mimeType)
    {
        if (source == null)
            throw new ArgumentException("source is null");
        return source.IsInventoryOnly(mimeType);
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
        var seed = rng.Next();
        var url = !useEncryption
            ? $"{simSageEndpoint}/crawler/external/crawler/log"
            : $"{simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            HttpPost(
                url, [
                    "objectType", "CMExternalLogEntry",
                    "organisationId", organisationId,
                    "kbId", kbId,
                    "sid", sid,
                    "sourceId", sourceId,
                    "logEntry", logEntry
                ], useEncryption, seed, simSageApiVersion, allowSelfSignedCertificate
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
        var s = source;
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
        var cleanUrl = isWindows ? RockUtils.Windows1252ToUtf8(url) : url;
        var cleanException = isWindows ? RockUtils.Windows1252ToUtf8(exception) : exception;

        // cleanWebUrl can be empty!
        var cleanWebUrl = isWindows ? RockUtils.Windows1252ToUtf8(webUrl) : webUrl;

        // check parameters before posting
        if (string.IsNullOrWhiteSpace(cleanUrl) || string.IsNullOrWhiteSpace(kbId) ||
            string.IsNullOrWhiteSpace(organisationId) || sourceId <= 0 || string.IsNullOrWhiteSpace(sid) || runId == 0L
        )
        {
            throw new ArgumentException("invalid parameter(s)");
        }

        numFilesSeen += 1;
        Logger.Debug($"recordExceptionAsset(url={cleanUrl},exception={cleanException},webUrl={cleanWebUrl})");
        var seed = rng.Next();
        var postUrl = !useEncryption
            ? $"{simSageEndpoint}/crawler/external/document/recordfailure"
            : $"{simSageEndpoint}/crawler/external/secure/{seed}";
        try
        {
            var jsonStr = HttpPost(
                postUrl, [
                    "objectType", "CMFailedSourceDocument",
                    "organisationId", organisationId,
                    "kbId", kbId,
                    "sid", sid,
                    "sourceId", sourceId,
                    "sourceSystemId", cleanUrl,
                    "webUrl", cleanWebUrl,
                    "deltaRootId", deltaRootId,
                    "runId", runId,
                    "errorMessage", cleanException
                ], useEncryption, seed, simSageApiVersion, allowSelfSignedCertificate
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
            Logger.Error($"recordExceptionAsset({url}): {ex.Message}");
        }
    }


    /// <summary>
    /// Periodically update the source
    /// </summary>
    private void CheckCrawler()
    {
        if (sourceNextRefreshTime < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            GetCrawlerFromDb();
        }
    }

    /// <summary>
    /// set a delta token on a source
    /// </summary>
    public void SetDeltaState(string deltaIndicator)
    {
        var cleanDelta = isWindows ? RockUtils.Windows1252ToUtf8(deltaIndicator) : deltaIndicator;
        Logger.Debug($"setDeltaState({cleanDelta})");
        var seed = rng.Next();
        var url = !useEncryption
            ? $"{simSageEndpoint}/crawler/external/crawler/delta-token"
            : $"{simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            var jsonStr = HttpPost(
                url,
                [
                    "objectType", "CMExternalCrawlerSetDeltaToken",
                    "organisationId", organisationId,
                    "kbId", kbId, "sid", sid, "sourceId", sourceId,
                    "deltaToken", cleanDelta
                ],
                useEncryption, seed, simSageApiVersion, allowSelfSignedCertificate
            );
            var data = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
            if (source != null)
                source.DeltaIndicator = deltaIndicator;
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

    public string GetDeltaState() => source?.DeltaIndicator ?? "";


    /// <summary>
    /// Processes the given asset by performing validation, encoding, and uploading it to the system.
    /// </summary>
    /// <param name="externalAsset">The asset to be processed, containing metadata such as URL and content details.</param>
    /// <returns>True if all is good and we should continue processing assets, False if the crawler needs to abort.</returns>
    public bool ProcessAsset(Asset externalAsset)
    {
        // upload - valid or invalid (i.e. data/mime-type or no mime-type - all valid
        numFilesUploaded += 1;

        // make sure the asset doesn't contain any bad windows characters
        var encodedAsset = EncodeAsset(externalAsset);

        CheckCrawler();

        // check we have the minimum requirements that the asset is valid
        if (encodedAsset.Url.Trim().Length == 0) {
            Logger.Error($"processAsset: asset url is empty, ignoring {encodedAsset.Url}");
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
            // set the item's data in cache
            _cacheDao.Set(externalAsset.Url, assetHash, cacheLifespanInDays * 3600_000L * 24L);
        }

        // is this an inventory-only asset?  don't send the bytes
        if (source?.IsInventoryOnly(externalAsset.MimeType) == true)
        {
            externalAsset.RemoveAssetTempFile();
        }

        var seed = (int)rng.NextInt64();

        // upload this file
        try {
            numFilesSeen += 1;

            FileUploadPost(
                organisationId, kbId, sid, sourceId,
                UploadExternalDocumentCmd.Convert(encodedAsset), externalAsset.Filename,
                useEncryption, runId, seed, simSageEndpoint, simSageApiVersion,
                FileUtils.MaximumSizeInBytesForMimeType(externalAsset.MimeType),
                allowSelfSignedCertificate
            );

        } catch (Exception ex) {
            Logger.Error($"processAsset({encodedAsset.Url}): {ex.Message}");
            numErrors += 1;
            return false;
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
        var cleanAsset = EncodeAsset(asset);
        Logger.Debug($"markFileAsSeen(url={cleanAsset.Url})");
        var seed = rng.Next();
        var postUrl = !useEncryption
            ? $"{simSageEndpoint}/crawler/external/crawler/mark-file-as-seen"
            : $"{simSageEndpoint}/crawler/external/secure/{seed}";
        try
        {
            var jsonStr = HttpPost(
                postUrl, [
                    "objectType", "CMExternalCrawlerMarkFileAsSeen",
                    "organisationId", organisationId,
                    "kbId", kbId, "sid", sid, "sourceId", sourceId,
                    "runId", runId,
                    "asset", cleanAsset
                ],
                useEncryption, seed, simSageApiVersion, allowSelfSignedCertificate
            );
            var data = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
            CheckError(data);
            numFilesSeen += 1;
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
        var cleanUrl = isWindows ? RockUtils.Windows1252ToUtf8(url) : url;
        Logger.Debug($"delete({cleanUrl})");
        var seed = rng.Next();
        var postUrl = !useEncryption
            ? $"{simSageEndpoint}/crawler/external/crawler/delete-url"
            : $"{simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            var jsonStr = HttpPost(
                postUrl, [
                    "objectType", "CMExternalCrawlerDeleteUrl",
                    "organisationId", organisationId,
                    "kbId", kbId, "sid", sid, "sourceId", sourceId,
                    "url", cleanUrl
                ],
                useEncryption, seed, simSageApiVersion, allowSelfSignedCertificate
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
        var cleanFolderUrl = isWindows ? RockUtils.Windows1252ToUtf8(folderUrl) : folderUrl;
        Logger.Debug($"deleteFolder({cleanFolderUrl})");
        var seed = rng.Next();
        var postUrl = !useEncryption
            ? $"{simSageEndpoint}/crawler/external/crawler/delete-folder"
            : $"{simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            var jsonStr = HttpPost(
                postUrl, [
                    "objectType", "CMExternalCrawlerDeleteFolder",
                    "organisationId", organisationId,
                    "kbId", kbId, "sid", sid, "sourceId", sourceId,
                    "folderUrl", cleanFolderUrl
                ],
                useEncryption, seed, simSageApiVersion, allowSelfSignedCertificate
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
            var seed = rng.Next();
            var postUrl = !useEncryption
                ? $"{simSageEndpoint}/crawler/external/crawler/rename-folder"
                : $"{simSageEndpoint}/crawler/external/secure/{seed}";

            try
            {
                var jsonStr = HttpPost(
                    postUrl, [
                        "objectType", "CMExternalCrawlerRenameFolder",
                        "organisationId", organisationId,
                        "kbId", kbId, "sid", sid, "sourceId", sourceId,
                        "oldFolderNameUrl", isWindows ? RockUtils.Windows1252ToUtf8(folder.OriginalFolderName) : folder.OriginalFolderName,
                        "newFolderNameUrl", isWindows ? RockUtils.Windows1252ToUtf8(folder.NewFolderName) : folder.NewFolderName,
                        "acls", EncodeAclList(folder.AssetAclList)
                    ],
                    useEncryption, seed, simSageApiVersion, allowSelfSignedCertificate
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
        var cleanAsset = EncodeAsset(asset);
        Logger.Debug($"markFileAsSeen(url={cleanAsset.Url})");
        var seed = rng.Next();
        var postUrl = !useEncryption
            ? $"{simSageEndpoint}/crawler/external/crawler/mark-file-as-seen"
            : $"{simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            var jsonStr = HttpPost(
                postUrl, [
                    "objectType", "CMExternalCrawlerMarkFileAsSeen",
                    "organisationId", organisationId,
                    "kbId", kbId, "sid", sid, "sourceId", sourceId,
                    "runId", runId,
                    "asset", cleanAsset
                ],
                useEncryption, seed, simSageApiVersion, allowSelfSignedCertificate
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
            Logger.Error($"markFileAsSeen({cleanAsset.Url}): {ex.Message}");
        }
    }

    /// <summary>
    /// Start the file crawler running
    /// </summary>
    public void CrawlerStart(ICrawler platformCrawler)
    {
        if (source?.IsExternal == false)
        {
            running = false;
            return;
        }
        
        // cleanup cache
        _cacheDao?.CleanupExpiredEntries();

        var crawler = source ?? throw new ArgumentException(NotLoaded);

        // check this crawler is a valid external crawler
        if (!crawler.IsExternal)
            throw new ArgumentException($"{crawler} is not set up as an external crawler");

        // set up a run-id
        runId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Logger.Info($"{crawler}, starting a new run for {runId}");

        // also signal the other pipe-lines that the crawler has started
        SignalCrawlerStart();

        // start crawling
        numFilesSeen = 0;
        numFilesUploaded = 0;
        numErrors = 0;
        running = true;
    }


    /// <summary>
    /// Is the system ready for the initial start
    /// This function will _wait_ until we're ready to start
    /// </summary>
    /// <returns><c>true</c> after we're ready to go, false if the crawler needs to stop / exit</returns>
    public bool WaitForStart()
    {
        if (source == null) return false;

        var currentSchedule = source?.Schedule ?? "";
        var currentScheduleEnabled = source?.ScheduleEnable ?? false;
        var waitTimeInHours = CrawlerUtils.CrawlerWaitTimeInHours(
            CrawlerUtils.GetCurrentTimeIndicatorString(),
            source?.Schedule ?? "",
            !running
        );
        var waitTimeInMilliseconds = (long)(waitTimeInHours * 3600_000L);
        if (waitTimeInMilliseconds > 0)
        {
            var prevTime = RockUtils.MilliSecondsDeltaToString(waitTimeInMilliseconds);
            var waitTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + waitTimeInMilliseconds;
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
                if (currentSchedule != (source?.Schedule ?? ""))
                    return false; // terminate crawler: schedule changed
                if (currentScheduleEnabled != (source?.ScheduleEnable ?? false))
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
        var waitTimeInHours = CrawlerUtils.CrawlerWaitTimeInHours(
            CrawlerUtils.GetCurrentTimeIndicatorString(),
            source?.Schedule ?? "",
            !running
        );
        var waitTimeInMilliseconds = (long)(waitTimeInHours * 3600_000L);
        var waitTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + waitTimeInMilliseconds;
        if (!(waitTimeInHours > 0)) return running;
        var currentSchedule = this.source?.Schedule ?? "";
        var prevTime = RockUtils.MilliSecondsDeltaToString(waitTimeInMilliseconds);
        Logger.Info($"{source?.Name}: waiting {prevTime} as per schedule");
        while (Active && waitTime > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() && source is { IsExternal: true })
        {
            for (var i = 0; i < 10 && Active; i++)
            {
                Thread.Sleep(1_000); // wait 10 seconds before checking again

            }

            if (!Active) break;
            // get any source changes - if false - we need to re-evaluate our status
            CheckCrawler();
            // if the schedule changes, the crawler is no longer "finished"
            if (source?.Schedule != currentSchedule)
            {
                currentSchedule = source?.Schedule ?? "";
                if (!running)
                    SignalCrawlerStart();
                running = true;
            }
            // abort if this is no longer an external crawler
            if (source?.IsExternal == false)
            {
                running = false;
                return false;
            }
            // re-evaluate our wait time - just in case
            waitTimeInHours = CrawlerUtils.CrawlerWaitTimeInHours(
                CrawlerUtils.GetCurrentTimeIndicatorString(),
                source?.Schedule ?? "",
                !running
            );
            waitTimeInMilliseconds = (long)(waitTimeInHours * 3600_000L);
            waitTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + waitTimeInMilliseconds;
            var nextTime = RockUtils.MilliSecondsDeltaToString(waitTimeInMilliseconds);
            if (prevTime != nextTime)
            {
                prevTime = nextTime;
                Logger.Info($"{source?.Name}: waiting {nextTime} as per schedule");
            }
        }
        return running;
    }

    public void CrawlerDone()
    {
        running = false;

        if (numFilesSeen > 0)
        {
            Logger.Info($"{source?.Name}, finished runId {runId}");
            // signal the crawler has finished to the pipe-line
            CrawlerFinished();
        }
        else
        {
            Logger.Warn($"crawler \"{source?.Name}\" didn't get any files, has finished run {runId}");
            // signal the crawler has finished to the pipe-line
            CrawlerFinished();
        }

        if (exitAfterFinishing)
        {
            Logger.Info($"crawler \"{source?.Name}\" exit after run finished (exit_after_crawl=true)");
            FileUtils.Shutdown(exitCode: 0);
        }

        WaitUntilCrawlerReady();
    }

    public void CrawlerCrashed(string reason)
    {
        running = false;
        numFilesSeen = 0;
        WaitUntilCrawlerReady();
    }

    public string GetCrawlerState()
    {
        return "";
    }

    public void SetCrawlerState(string state)
    {
        // Not implemented for this proxy
    }

    /// <summary>
    /// Signal an external crawler is starting
    /// </summary>
    private void SignalCrawlerStart()
    {
        Logger.Debug("signalCrawlerStart()");
        var seed = rng.Next();
        var url = !useEncryption
            ? $"{simSageEndpoint}/crawler/external/crawler/start"
            : $"{simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            var jsonStr = HttpPost(
                url, [
                    "objectType", "CMExternalCrawlerStart", "organisationId", organisationId,
                    "kbId", kbId, "sid", sid, "sourceId", sourceId, "runId", runId
                ], useEncryption, seed, simSageApiVersion, allowSelfSignedCertificate
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
        Logger.Info($"{source}, has finished run {runId}");

        var seed = rng.Next();
        var url = !useEncryption
            ? $"{simSageEndpoint}/crawler/external/crawler/finish"
            : $"{simSageEndpoint}/crawler/external/secure/{seed}";

        try
        {
            var jsonStr = HttpPost(
                url, [
                    "objectType", "CMExternalCrawlerStop", "organisationId", organisationId,
                    "kbId", kbId, "sid", sid, "sourceId", sourceId, "numErrors", numErrors, "runId", runId,
                    "numFilesSeen", numFilesSeen
                ], useEncryption, seed, simSageApiVersion, allowSelfSignedCertificate
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
    /// the windows platform has some strange encodings Windows-1252 - convert an asset if necessary
    /// </summary>
    private static Asset EncodeAsset(Asset asset)
    {
        if (!RockUtils.IsWindows())
        {
            return asset;
        }
        var newAsset = new Asset
        {
            Url = RockUtils.Windows1252ToUtf8(asset.Url),
            ParentUrl = RockUtils.Windows1252ToUtf8(asset.ParentUrl),
            MimeType = asset.MimeType,
            DeltaRootId = asset.DeltaRootId,
            Title = RockUtils.Windows1252ToUtf8(asset.Title),
            Author = RockUtils.Windows1252ToUtf8(asset.Author),
            Template = RockUtils.Windows1252ToUtf8(asset.Template),
            BinarySize = asset.BinarySize,
            Created = asset.Created,
            LastModified = asset.LastModified,
            Filename = asset.Filename,
            PreviewImage = asset.PreviewImage
        };

        foreach (var key in asset.Metadata.Keys)
        {
            var value = asset.Metadata[key];
            var newKey = RockUtils.Windows1252ToUtf8(key);
            var newValue = RockUtils.Windows1252ToUtf8(value);
            newAsset.Metadata[newKey] = newValue;
        }

        newAsset.Acls = EncodeAclList(asset.Acls).ToList();

        return newAsset;
    }

    /// <summary>
    /// the windows platform has some strange encodings Windows-1252 - convert a list of acls if necessary
    /// </summary>
    private static List<AssetAcl> EncodeAclList(List<AssetAcl> acls)
    {
        var newAclList = new List<AssetAcl>();
        foreach (var acl in acls)
        {
            newAclList.Add(EncodeAcl(acl));
        }
        return newAclList;
    }

    /// <summary>
    /// the windows platform has some strange encodings Windows-1252 - convert an acl if necessary
    /// </summary>
    private static AssetAcl EncodeAcl(AssetAcl acl)
    {
        if (!RockUtils.IsWindows())
        {
            return acl;
        }
        var newAcl = new AssetAcl
        {
            Name = RockUtils.Windows1252ToUtf8(acl.Name),
            DisplayName = RockUtils.Windows1252ToUtf8(acl.DisplayName),
            Access = RockUtils.Windows1252ToUtf8(acl.Access)
        };
        var newMemberList = new List<string>();
        foreach (var member in acl.MembershipList)
        {
            newMemberList.Add(RockUtils.Windows1252ToUtf8(member));
        }
        newAcl.MembershipList = newMemberList;
        return newAcl;
    }

    /// <summary>
    /// Upload a file to SimSage (with or without payload)
    /// </summary>
    private static void FileUploadPost(
        string organisationId, string kbId, string sid, int sourceId,
        UploadExternalDocumentCmd file, string filename, bool useEncryption, long runId, int seed,
        string simSageEndpoint, string simSageApiVersion, long maxSizeInBytes, bool allowSelfSignedCertificate
    )
    {
        var tempFile = !string.IsNullOrEmpty(filename) ? new FileInfo(filename) : null;
        var data = tempFile?.Exists == true ? File.ReadAllBytes(filename) : [];
        Logger.Debug($"fileUploadPost(url={file.Url},size={data.Length})");
        try
        {
            var url = !useEncryption
                ? $"{simSageEndpoint}/crawler/external/document/upload"
                : $"{simSageEndpoint}/crawler/external/secure/{seed}";
            var base64Data = "";
            // never post more data than we're supposed to
            if (data.Length > 0 && data.Length < maxSizeInBytes)
            {
                base64Data = Base64Prefix + Convert.ToBase64String(data);
            }

            var jsonStr = HttpPost(
                url, [
                    "objectType", "CMUploadDocument", "organisationId", organisationId, "kbId", kbId, "sid", sid,
                    "sourceId", sourceId, "url", file.Url, "mimeType", file.MimeType, "runId", runId,
                    "acls", AssetAcl.UniqueAcls(file.Acls), "title", file.Title, "author", file.Author,
                    "changeHash", file.ChangeHash, "contentHash", file.ContentHash,
                    "data", base64Data, "created", file.Created, "lastModified", file.LastModified,
                    "size", file.Size, "metadata", file.Metadata, "categories", file.Categories, "puid", file.Puid,
                    "template", file.Template, "binarySize", file.BinarySize
                ], useEncryption, seed, simSageApiVersion, allowSelfSignedCertificate
            );
            var data2 = Mapper.ReadValue<Dictionary<string, object>>(jsonStr);
            CheckError(data2);
        }
        finally
        {
            tempFile?.Delete();
        }
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
        string str;
        HttpResponseMessage? response = null;
        HttpClient? client = null;
        var objectType = "";
        var encryptionKey = "";
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
                encryptionKey = Sha512.GenerateSha512Hash(_aes, SharedSecrets.GetRandomGuid(_aes, seed).ToString());
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

            response = client.SendAsync(request).Result; // Synchronous call for simplicity, use async in real app
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
            throw new ArgumentException($"could not read POST contents of {url}, (cmd:{objectType}) exception: ({ex.Message})", ex);
        }
        finally
        {
            response?.Dispose();
            client?.Dispose();
        }

        if (!string.IsNullOrWhiteSpace(str) && useEncryption && !str.StartsWith("{") && !str.EndsWith("}"))
        {
            return AesEncryption.Decrypt(
                str,
                Sha512.GenerateSha512Hash(_aes, SharedSecrets.GetRandomGuid(_aes, seed).ToString())
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
            }
            throw new ArgumentException($"POST: error {(int)response.StatusCode}, {url}");
        }
    }

    string ICrawlerApi.GetDeltaState()
    {
        return source?.DeltaIndicator ?? "";
    }

    
}
