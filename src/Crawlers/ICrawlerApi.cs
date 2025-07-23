namespace Crawlers;

public interface ICrawlerApi
{
    void SetDeltaState(string deltaState);
    string GetDeltaState();
    bool ProcessAsset(Asset asset);
    void MarkFileAsSeen(Asset asset);
    bool HasExceededCapacity();
    bool IsInventoryOnly(string mimeType);
    void VerifyParameters(string name, Dictionary<string, object> propertyMap, List<string> propertyNameList);
}
