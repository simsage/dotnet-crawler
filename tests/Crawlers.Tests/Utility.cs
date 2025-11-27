namespace Crawlers.Tests;

public class Utility
{
    public static PlatformCrawlerCommonProxy CreatePlatformCrawler(bool sourceInventoryFlag)
    {
        var pc = new PlatformCrawlerCommonProxy(
            "test service",
            "https://local.com",
            "1",
            "FILE",
            "org-id",
            "kb-id",
            "sid",
            "aes",
            1,
            false,
            true,
            true,
            false
        );
        var source = new Source();
        source.InventoryOnlyInclude = sourceInventoryFlag;
        source.InventoryOnlyMimeTypes = ["application/pdf"];
        pc.SetSource(source); 
        return pc;
    }

    public static Asset CreateAsset(string url, long size, string mimeType)
    {
        var asset = new Asset();
        asset.Url = url;
        asset.BinarySize = size;
        asset.MimeType = mimeType;
        return asset;
    }
    
}