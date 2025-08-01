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

    // it is sat, just past midnight, the crawler has just finished.
    // the next slot in this series is sat-03, meaning we need to wait 3 hours
    [Fact]
    public void ScheduleTest1() {
        // wait 3 hours
        Assert.Equal(3, CrawlerUtils.CrawlerWaitTimeInHours("sat-00", "sat-00,sat-01,sat-3", true));
        // wait 10 hours
        Assert.Equal(10, CrawlerUtils.CrawlerWaitTimeInHours("sat-00", "sat-00,sat-01,sat-10", true));
    }

    [Fact]
    public void ScheduleTest2() {
        // wait 6 days and 3 hours = 144 + 3
        Assert.Equal(147, CrawlerUtils.CrawlerWaitTimeInHours("sat-00", "sat-00,sat-01,fri-03", true));
        // same
        Assert.Equal(147, CrawlerUtils.CrawlerWaitTimeInHours("sun-00", "sun-00,sat-03", true));
        // same
        Assert.Equal(147, CrawlerUtils.CrawlerWaitTimeInHours("mon-01", "mon-01,sun-04", true));
    }


    [Fact]
    public void ScheduleTest3() {
        // wait 3 hours to the next slot
        Assert.Equal(3, CrawlerUtils.CrawlerWaitTimeInHours("sat-00", "sat-03", true));
    }

}
