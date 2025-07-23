namespace Crawlers.Tests;

public class TestSqliteCacheWithTtl
{
    [Fact]
    public void CacheTest1()
    {
        var tempFile = FileUtils.GetTempFilename("db");
        var cache = new SqliteAssetDao("test-service", tempFile);
        cache.CacheDatabasePath = FileUtils.GetTempFilename("db");
        cache.Initialize();
        cache.CleanupExpiredEntries();

        Assert.False(cache.ContainsKey("key1"));
        cache.Set("key1", "value1", 200);
        Assert.Equal("value1", cache.Get("key1"));

        Thread.Sleep(210);
        cache.CleanupExpiredEntries();
        Assert.False(cache.ContainsKey("key1"));

        // remove after use
        File.Delete(cache.CacheDatabasePath);
    }
    
}