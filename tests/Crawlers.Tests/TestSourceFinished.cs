namespace Crawlers.Tests;

public class TestSourceFinished
{
    // initial conditions - always ready / finished
    [Fact]
    public void TestFinished1()
    {
        var source = new Source
        {
            StartTime = 0,
            EndTime = 0,
            EndTimeCrawler = 0
        };
        Assert.True(PlatformCrawlerCommonProxy.DidCrawlFinish(source));
    }


    // when start == 0 => always finished
    [Fact]
    public void TestFinished2()
    {
        var source = new Source
        {
            StartTime = 0,
            EndTime = 12351,
            EndTimeCrawler = 23
        };
        Assert.True(PlatformCrawlerCommonProxy.DidCrawlFinish(source));
    }


    // when start == 0 => always finished
    [Fact]
    public void TestFinished3()
    {
        var source = new Source
        {
            StartTime = 12350,
            EndTime = 12351,
            EndTimeCrawler = 12351
        };
        Assert.True(PlatformCrawlerCommonProxy.DidCrawlFinish(source));
    }


    // crawler not finished
    [Fact]
    public void TestFinished4()
    {
        var source = new Source
        {
            StartTime = 12350,
            EndTime = 0,
            EndTimeCrawler = 0
        };
        Assert.False(PlatformCrawlerCommonProxy.DidCrawlFinish(source));
    }


    // all files processed, because was never started
    [Fact]
    public void TestFinished5()
    {
        var source = new Source
        {
            StartTime = 0,
            EndTime = 0,
            EndTimeCrawler = 0
        };
        Assert.True(PlatformCrawlerCommonProxy.HaveAllFilesProcessed(source));
    }


    // all files processed, because was never started
    [Fact]
    public void TestFinished6()
    {
        var source = new Source
        {
            StartTime = 0,
            EndTime = 123,
            EndTimeCrawler = 124
        };
        Assert.True(PlatformCrawlerCommonProxy.HaveAllFilesProcessed(source));
    }

    // not finished yet
    [Fact]
    public void TestFinished7()
    {
        var source = new Source
        {
            StartTime = 123,
            EndTimeCrawler = 125,
            EndTime = 100
        };
        Assert.False(PlatformCrawlerCommonProxy.HaveAllFilesProcessed(source));
    }

    // finished all!
    [Fact]
    public void TestFinished8()
    {
        var source = new Source
        {
            StartTime = 123,
            EndTimeCrawler = 125,
            EndTime = 125
        };
        Assert.True(PlatformCrawlerCommonProxy.HaveAllFilesProcessed(source));
    }

    // finished all!
    [Fact]
    public void TestFinished9()
    {
        var source = new Source
        {
            StartTime = 123,
            EndTimeCrawler = 125,
            EndTime = 125000
        };
        Assert.True(PlatformCrawlerCommonProxy.HaveAllFilesProcessed(source));
    }

}
