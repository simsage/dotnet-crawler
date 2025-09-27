using System.Diagnostics.SymbolStore;
using System.Drawing;

namespace Crawlers.Tests;

public class TestInventoryOnly
{
    [Fact]
    public void TestInventoryOnly1()
    {
        var pc = CreatePlatformCrawler(false);
        Assert.True(pc.IsInventoryOnly(
            CreateAsset(
                "https://some.server.com/data.pdf",
                1024L,
                "application/pdf"
            )
        ));
    }

    // too big
    [Fact]
    public void TestInventoryOnly2()
    {
        var pc = CreatePlatformCrawler(false);
        Assert.True(pc.IsInventoryOnly(
            CreateAsset(
                "https://some.server.com/data.docx",
                1231234123123123123L,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            )
        ));
    }

    // zero size
    [Fact]
    public void TestInventoryOnly3()
    {
        var pc = CreatePlatformCrawler(false);
        Assert.True(pc.IsInventoryOnly(
            CreateAsset(
                "https://some.server.com/data.docx",
                0L,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            )
        ));
    }

    // invalid type
    [Fact]
    public void TestInventoryOnly4()
    {
        var pc = CreatePlatformCrawler(false);
        Assert.True(pc.IsInventoryOnly(
            CreateAsset(
                "https://some.server.com/data",
                1024L,
                ""
            )
        ));
    }

    // source says it is
    [Fact]
    public void TestInventoryOnly5()
    {
        var pc = CreatePlatformCrawler(true);
        Assert.True(pc.IsInventoryOnly(
            CreateAsset(
                "https://some.server.com/data.docx",
                1024L,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            )
        ));
    }

    // valid - doesn't need to go to inventory
    [Fact]
    public void TestInventoryOnly6()
    {
        var pc = CreatePlatformCrawler(false);
        Assert.False(pc.IsInventoryOnly(
            CreateAsset(
                "https://some.server.com/data.docx",
                1024L,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            )
        ));
    }

    /////////////////////////////////////////////////////////////////////
    
    private PlatformCrawlerCommonProxy CreatePlatformCrawler(bool sourceInventoryFlag)
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

    private Asset CreateAsset(string url, long size, string mimeType)
    {
        var asset = new Asset();
        asset.Url = url;
        asset.BinarySize = size;
        asset.MimeType = mimeType;
        return asset;
    }
    
}