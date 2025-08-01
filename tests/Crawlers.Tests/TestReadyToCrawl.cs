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

}
