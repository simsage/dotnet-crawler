namespace Crawlers;

public interface ICrawlerApi
{
    void SetDeltaState(string deltaState);
    string GetDeltaState();
    bool ProcessAsset(Asset asset);
    void MarkFileAsSeen(Asset asset);
    bool HasExceededCapacity();
    bool IsInventoryOnly(Asset asset);
    void VerifyParameters(string name, Dictionary<string, object> propertyMap, List<string> propertyNameList);

    // if the cache is enabled, this method will return true if the asset has been modified since the last time it was seen
    // if the cache is disabled, it will return true (i.e., it then can't assume anything)
    bool LastModifiedHasChanged(Asset asset);
}
