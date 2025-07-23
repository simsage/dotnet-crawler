namespace Crawlers;

using System.Collections.Generic;

public interface ICrawler
{
    /// <summary>
    /// initialize a crawler with the properties required to make it work
    /// </summary>
    /// <param name="name">the name of the crawler for logging</param>
    /// <param name="propertyMap">set of credentials and properties to make this crawler work
    ///               these can be anything, just put in here what is required
    ///               e.g. credential, user_name, location, etc.
    ///               you can define these yourself.  Please use JavaScript friendly names in this map
    ///               your implementation should read these values and use them to connect to your external system
    ///               please do not put OBJECT inside this map, keep it simple - just String -> Int, or String -> String
    ///               or String -> Boolean</param>
    /// <param name="api">the part of SimSage to interact with</param>
    void Initialize(string name, Dictionary<string, object> propertyMap, ICrawlerApi api);

    /// <summary>
    /// run the crawler - returns true when you have finished a single run through all the data
    /// returns false only on fail (i.e. the external system no longer exists, or SimSage has signalled !active
    ///         or SimSage returns false from processAsset())
    /// </summary>
    bool Run();

    void SetDeltaState(string deltaState);
    
    /// <summary>
    /// test the crawler's connectivity given the initialized properties
    /// returns true if this crawler can confirm connectivity with an external system
    /// must throw an IllegalArgumentException() exception (which is caught by SimSage) if it fails
    /// with the reason why it failed
    /// </summary>
    bool Test();

    /// <summary>
    /// OIDC aware crawlers have an OIDC refresh token
    /// this call updates the internal refreshToken of an active crawler in case it has changed
    /// </summary>
    /// <param name="refreshToken">the token to set / update</param>
    void UpdateRefreshToken(string refreshToken);

    /// <summary>
    /// OIDC aware crawlers have an OIDC refresh token
    /// </summary>
    /// <returns>the current refresh-token for this instance of a crawler</returns>
    string GetCurrentRefreshToken();

    /// <summary>
    /// Save the crawler's state to JSON
    /// </summary>
    string StateToJson();

    /// <summary>
    /// Restore the crawler's state from JSON
    /// </summary>
    void StateFromJson(string json);

    /// <summary>
    /// Clear crawler's state (reset)
    /// </summary>
    void ClearState();
}
