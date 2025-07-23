namespace Crawlers;

public class Document
{
    /**
     * get a file-type / extension from an uri without the '.' or empty if dne / too big
     */
    public static string GetFileExtension(string url)
    {
        // is it just a base URL?
        var colonIndex = url.IndexOf("://");
        if (colonIndex >= 0)
        {
            // no next slash after? doesn't have a file-extension
            if (url.IndexOf('/', colonIndex + 3) < 0)
                return "";
        }

        // Assuming Source.removeQueryString and Source.removeHashFromUrl are implemented elsewhere.
        // For demonstration, let's provide a basic implementation or assume they exist.
        // If they are not available, you'll need to implement them or use existing .NET URL parsing.
        var url2 = RemoveQueryString(RemoveHashFromUrl(url)).Trim();

        var index = url2.LastIndexOf('.');
        var index2 = url2.LastIndexOf('/');
        var index3 = url2.LastIndexOf('\\');
        if (index3 > index2)
        {
            index2 = index3;
        }

        if (index > 0 && index > index2 && index + 1 < url.Length)
        {
            return url2.Substring(index + 1).ToLowerInvariant().Trim();
        }

        return url.Length <= 5 ? url.ToLowerInvariant().Trim() : "";
    }

    //region Helper Methods (to mimic Kotlin's Source.removeQueryString and Source.removeHashFromUrl)
    // You would typically have these in a utility class or use Uri class for robust URL parsing.

    private static string RemoveQueryString(string url)
    {
        int queryIndex = url.IndexOf('?');
        if (queryIndex >= 0)
        {
            return url.Substring(0, queryIndex);
        }
        return url;
    }

    private static string RemoveHashFromUrl(string url)
    {
        int hashIndex = url.IndexOf('#');
        if (hashIndex >= 0)
        {
            return url.Substring(0, hashIndex);
        }
        return url;
    }
            
    public static string META_BODY = "{body}";
    public static string META_TITLE = "{title}";
    public static string META_AUTHOR = "{author}";
    public static string META_URL = "{url}";
    public static string META_FILENAME = "{filename}";
    public static string META_FOLDER = "{folder}";
    public static string META_LANGUAGE = "{language}";                  // the language of the document (e.g. English)
    public static string META_CREATED_DATE_TIME = "{created}";
    public static string META_LAST_MODIFIED_DATE_TIME = "{lastmod}";
    public static string META_TEMPLATE = "{template}";
    public static string META_HASHTAG = "{hashtag}";
    public static string META_DOCUMENT_TYPE = "{doctype}";
    public static string META_SUMMARY = "{summary}";

}
