namespace Crawlers;

using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

    public class SqliteAssetDao : IDisposable
    {
        private static readonly RockLogger Logger = RockLogger.GetLogger(typeof(SqliteAssetDao));
        private CachedAssetDbContext DbContext { get; }
        public string CacheDatabasePath { get; set; }

        public SqliteAssetDao(string serviceName)
        {
            // Define your cache database file path
            if (string.IsNullOrEmpty(CacheDatabasePath))
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var serviceDataPath = Path.Combine(appDataPath, serviceName);
                Directory.CreateDirectory(serviceDataPath); // Ensure directory exists
                CacheDatabasePath = Path.Combine(serviceDataPath, "crawler_cache.db");
            }
            DbContext = new CachedAssetDbContext(CacheDatabasePath);
        }

        public SqliteAssetDao(string serviceName, string databasePath)
        {
            CacheDatabasePath = databasePath;
            DbContext = new CachedAssetDbContext(CacheDatabasePath);
        }

        /// <summary>
        /// Ensures the database is created and any pending migrations are applied.
        /// Call this once when the application starts.
        /// </summary>
        public void Initialize() 
        {
            try
            {
                DbContext.Database.Migrate();
                EnsureTableExistsWithRawSql();
                // perform a test get after initialization
                var firstOrDefault = DbContext.CachedAsset.AsNoTracking()
                    .FirstOrDefault(e => e.Key == "test-key");
                Logger.Info($"SqliteAssetDao initialized. test-key={firstOrDefault}");
            }
            catch (Exception ex)
            {
                // Log the error. The service will typically handle the EventLog entry.
                Logger.Error($"Initialize() {ex.Message}");
                throw; // Re-throw to indicate initialization failure
            }
        }

        // Renamed Initialize to something like EnsureTableExistsWithRawSql
        // to highlight it's not the standard EF Migrate.
        private void EnsureTableExistsWithRawSql()
        {
            try
            {
                // This is the SQL command to create the table IF IT DOES NOT EXIST.
                // The column types (TEXT, INTEGER) are SQLite-specific.
                // Make sure this matches your CacheEntry model.
                const string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS CachedAsset (
                        Key TEXT NOT NULL PRIMARY KEY,
                        Value TEXT NOT NULL,
                        ExpiresAt BIGINT NOT NULL
                    );";

                // Execute the raw SQL command
                DbContext.Database.ExecuteSqlRaw(createTableSql);
            }
            catch (Exception ex)
            {
                Logger.Error($"EnsureTableExistsWithRawSql() {ex.Message}");
                throw; // Re-throw to indicate initialization failure
            }
        }


        /// <summary>
        /// Retrieves a cache entry by its key. Returns null if not found or expired.
        /// </summary>
        /// <param name="key">The unique key of the cache entry.</param>
        /// <returns>The cached value as a string, or null.</returns>
        public string Get(string key) // Changed from async Task<string> to string
        {
            try
            {
                // Synchronous FirstOrDefault
                var entry = DbContext.CachedAsset.AsNoTracking()
                                            .FirstOrDefault(e => e.Key == key);

                if (entry != null)
                {
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (entry.ExpiresAt > now)
                    {
                        // For a synchronous simple cache, often not updated on read for performance.
                        return entry.Value;
                    }
                    else
                    {
                        // Expired, remove it
                        Remove(key); // Call synchronous remove
                    }
                }
                return "";
            }
            catch (Exception ex)
            {
                Logger.Error($"Get() {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Check if a cache entry is in the db
        /// </summary>
        /// <param name="key">The unique key of the cache entry.</param>
        /// <returns>true if the entry exists.</returns>
        public bool ContainsKey(string key)
        {
            try
            {
                // Synchronous FirstOrDefault
                var entry = DbContext.CachedAsset.AsNoTracking()
                    .FirstOrDefault(e => e.Key == key);

                return (entry != null);
            }
            catch (Exception ex)
            {
                Logger.Error($"Get() {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets or updates a cache entry.
        /// </summary>
        /// <param name="key">The unique key of the cache entry.</param>
        /// <param name="value">The value to cache.</param>
        /// <param name="lifeSpanInMilliseconds">The duration for which the entry should be cached.</param>
        public void Set(string key, string value, long lifeSpanInMilliseconds) // Changed from async Task to void
        {
            try
            {
                // Synchronous FirstOrDefault
                var existingEntry = DbContext.CachedAsset.FirstOrDefault(e => e.Key == key);

                if (existingEntry != null)
                {
                    existingEntry.Value = value;
                    existingEntry.ExpiresAt = DateTimeOffset.UtcNow.Add(TimeSpan.FromMilliseconds(lifeSpanInMilliseconds)).ToUnixTimeMilliseconds();
                }
                else
                {
                    var newEntry = new CachedAsset(key, value, lifeSpanInMilliseconds);
                    DbContext.CachedAsset.Add(newEntry);
                }
                DbContext.SaveChanges(); // Synchronous SaveChanges
            }
            catch (Exception ex)
            {
                Logger.Error($"Set() {ex.Message}");
            }
        }

        /// <summary>
        /// Removes a cache entry by its key.
        /// </summary>
        /// <param name="key">The key of the entry to remove.</param>
        private void Remove(string key) // Changed from async Task to void
        {
            try
            {
                // Synchronous FirstOrDefault
                var entry = DbContext.CachedAsset.FirstOrDefault(e => e.Key == key);
                if (entry != null)
                {
                    DbContext.CachedAsset.Remove(entry);
                    DbContext.SaveChanges(); // Synchronous SaveChanges
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Remove() {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up all expired cache entries.
        /// </summary>
        public void CleanupExpiredEntries() // Changed from async Task to void
        {
            try
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                // Synchronous ToList
                var expiredEntries = DbContext.CachedAsset
                                                    .Where(e => e.ExpiresAt <= now)
                                                    .ToList(); // Synchronous ToList
                if (expiredEntries.Any())
                {
                    DbContext.CachedAsset.RemoveRange(expiredEntries);
                    DbContext.SaveChanges(); // Synchronous SaveChanges
                    Logger.Debug($"[Cache DAO] Cleaned up {expiredEntries.Count} expired cache entries.");
                    // Log to EventLog from the service layer, not DAO
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"CleanupExpiredEntries() {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes the DbContext.
        /// </summary>
        public void Dispose()
        {
            DbContext.Dispose();
        }
    }
    