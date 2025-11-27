using System.Diagnostics.SymbolStore;
using System.Drawing;

namespace Crawlers.Tests;

public class TestInventoryOnly
{
    [Fact]
    public void TestInventoryOnly1()
    {
        var pc = Utility.CreatePlatformCrawler(false);
        Assert.True(pc.IsInventoryOnly(
            Utility.CreateAsset(
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
        var pc = Utility.CreatePlatformCrawler(false);
        Assert.True(pc.IsInventoryOnly(
            Utility.CreateAsset(
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
        var pc = Utility.CreatePlatformCrawler(false);
        Assert.True(pc.IsInventoryOnly(
            Utility.CreateAsset(
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
        var pc = Utility.CreatePlatformCrawler(false);
        Assert.True(pc.IsInventoryOnly(
            Utility.CreateAsset(
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
        var pc = Utility.CreatePlatformCrawler(true);
        Assert.True(pc.IsInventoryOnly(
            Utility.CreateAsset(
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
        var pc = Utility.CreatePlatformCrawler(false);
        Assert.False(pc.IsInventoryOnly(
            Utility.CreateAsset(
                "https://some.server.com/data.docx",
                1024L,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            )
        ));
    }

    // invalid file type - straight into inventory
    [Fact]
    public void TestInventoryOnly7()
    {
        var pc = Utility.CreatePlatformCrawler(false);
        Assert.True(pc.IsInventoryOnly(
            Utility.CreateAsset(
                "https://some.server.com/data.fcs",
                3024L,
                "application/vnd.isac.fcs"
            )
        ));
    }
   
    
}