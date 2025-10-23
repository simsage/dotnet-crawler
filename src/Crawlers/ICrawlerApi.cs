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

    /// <summary>
    /// Record an asset exception
    /// </summary>
    /// <param name="asset">The asset/document data.</param>
    /// <param name="errorStr">The error to log.</param>
    /// <param name="exception">Optional exception information.</param>
    /// <returns>A string representing the downloaded file path, or an empty string if the file is not downloaded.</returns>
    void RecordAssetException(Asset asset, string errorStr, Exception? exception);

    // if the cache is enabled, this method will return true if the asset has been modified since the last time it was seen
    // if the cache is disabled, it will return true (i.e., it then can't assume anything)
    bool LastModifiedHasChanged(Asset asset);
}
