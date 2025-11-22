namespace Crawlers.Tests;

public class TestReadyToCrawl
{
    [Fact]
    public void TestNextTimeSlow()
    {
        var currentSchedule = CrawlerUtils.GetCurrentTimeIndicatorString();
        var timeSet = new HashSet<string>();
        var time = currentSchedule;
        var i = 0;
        while (i < 24 * 7)
        {
            timeSet.Add(time);
            time = CrawlerUtils.GetNextTimeSlot(time);
            Assert.Equal(6, time.Length);
            i += 1;
        }
        Assert.Equal(24 * 7, timeSet.Count);
    }

    [Fact]
    public void ScheduleTest1() {
        // start immediately
        Assert.Equal(0, CrawlerUtils.CrawlerWaitTimeInHours("sat-00", "sat-00,sat-01,sat-3"));
        // wait 10 hours
        Assert.Equal(10, CrawlerUtils.CrawlerWaitTimeInHours("sat-00", "sat-10"));
    }

    [Fact]
    public void ScheduleTest2() {
        // wait 6 days and 3 hours = 144 + 3
        Assert.Equal(147, CrawlerUtils.CrawlerWaitTimeInHours("sat-00", "fri-03"));
        // same
        Assert.Equal(147, CrawlerUtils.CrawlerWaitTimeInHours("sun-00", "sat-03"));
        // same
        Assert.Equal(147, CrawlerUtils.CrawlerWaitTimeInHours("mon-01", "sun-04"));
    }


    [Fact]
    public void ScheduleTest3() {
        // wait 3 hours to the next slot
        Assert.Equal(3, CrawlerUtils.CrawlerWaitTimeInHours("sat-00", "sat-03"));
    }

    [Fact]
    public void ScheduleTest4() {
        Assert.Equal(24 * 36500, CrawlerUtils.CrawlerWaitTimeInHours("sat-00", ""));
    }

}
