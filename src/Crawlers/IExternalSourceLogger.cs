namespace Crawlers;

public interface IExternalSourceLogger
{
    public void TransmitLogEntryToPlatform(string logEntry);
}
