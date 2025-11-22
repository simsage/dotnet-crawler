namespace Crawlers;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public abstract class RenameFolderAsset
{
    [JsonPropertyName("originalFolderName")]
    public string OriginalFolderName { get; set; } = string.Empty;
    [JsonPropertyName("newFolderName")]
    public string NewFolderName { get; set; } = string.Empty;

    [JsonPropertyName("assetAclList")] 
    public List<AssetAcl> AssetAclList { get; set; } = [];
}

public class MetadataMapping
{
    [JsonPropertyName("extMetadata")]
    public string ExtMetadata { get; set; } = string.Empty;
    [JsonPropertyName("metadata")]
    public string Metadata { get; set; } = string.Empty;
    [JsonPropertyName("display")]
    public string Display { get; set; } = string.Empty;

    public MetadataMapping(string extMetadata, string metadata, string display)
    {
        ExtMetadata = extMetadata;
        Metadata = metadata;
        Display = display;
    }
}

// JsonSerializer
public class JsonSerializer
{
    public static JsonSerializer Create() => new();
    public string WriteValueAsString(object obj) => System.Text.Json.JsonSerializer.Serialize(obj);
    public T? ReadValue<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json);
}


// Vars - read data from the environment settings
public class Vars
{
    public static string Get(string key) => Environment.GetEnvironmentVariable(key) ?? "";
}

// CrawlerUtils - help the crawler with various functions
public abstract class CrawlerUtils
{
    /// <summary>
    /// helper - split a schedule time string into its parts and return them in a set
    /// </summary>
    /// <param name="scheduleTime">the string to break apart around commas</param>
    /// <returns>a set of the time items inside that string, normalized to day-hh</returns>
    public static HashSet<string> GetTimeSlots(string scheduleTime)
    {
        var resultSet = new HashSet<string>();
        foreach (var item in scheduleTime.Split(","))
        {
            if (item.Trim().Length == 0) continue;
            var itemStr = item.Trim();
            if (itemStr.Length == 6)
            {
                // mon-00
                resultSet.Add(itemStr);
            }
            else if (itemStr.Length == 5)
            {
                // mon-0
                var parts = itemStr.Split("-");
                if (parts.Length == 2 && parts[0].Length == 3 && parts[1].Length == 1)
                {
                    resultSet.Add(parts[0] + "-0" + parts[1]);
                }
            }
        }
        return resultSet;
    }


    /// <summary>
    /// take a current time-slot (e.g., "mon-00") and return the next slot one hour later
    /// </summary>
    /// <param name="timeSlot">a value like mon-00</param>
    /// <returns>a value like mon-01</returns>
    public static string GetNextTimeSlot(string timeSlot)
    {
        // bad format?
        if (timeSlot.Length != 6)
            return "sun-00";
        // check the format in depth
        var parts = timeSlot.Split("-");
        if (parts.Length != 2 || parts[0].Length != 3 || parts[1].Length != 2)
            return "sun-00";
        var dow = parts[0]; // day of week
        var hod = parts[1]; // hour of day
        if (!int.TryParse(hod, out int hodInt))
            return "sun-00";
        if (hodInt < 0 || hodInt > 23)
            return "sun-00";
        if (dow != "mon" && dow != "tue" && dow != "wed" && dow != "thu" && dow != "fri" && dow != "sat" && dow != "sun")
            return "sun-00";
        if (hodInt == 23)
        {
            if (dow == "sun") return "mon-00";
            if (dow == "mon") return "tue-00";
            if (dow == "tue") return "wed-00";
            if (dow == "wed") return "thu-00";
            if (dow == "thu") return "fri-00";
            if (dow == "fri") return "sat-00";
            return "sun-00";
        }
        var nextHod = hodInt + 1;
        if (nextHod < 10)
            return dow + "-0" + nextHod;
        return dow + "-" + nextHod;
    }


    /// <summary>
    /// determine how many hours it is until we should crawl again according to the source
    /// </summary>
    /// <param name="currentTimeStr">the current time slot we're at</param>
    /// <param name="scheduleTime">the schedule of the source</param>
    /// <returns></returns>
    public static int CrawlerWaitTimeInHours(string currentTimeStr, string scheduleTime)
    {
        var scheduleSet = GetTimeSlots(scheduleTime);

        // no schedule? then wait 100 years
        if (scheduleSet.Count == 0)
            return 24 * 36500;

        // running in an active slot - if the crawler is active, go for it!
        if (scheduleSet.Contains(currentTimeStr))
        {
            return 0;
        }

        // count how long until we find an open slot
        var nextTimeSlot = GetNextTimeSlot(currentTimeStr);
        var numSlots = 1;
        while (scheduleSet.Contains(nextTimeSlot) && numSlots < 24 * 7)
        {
            nextTimeSlot = GetNextTimeSlot(nextTimeSlot);
            numSlots += 1;
        }
        // and find the next open-slot again
        while (!scheduleSet.Contains(nextTimeSlot) && numSlots < 24 * 7)
        {
            nextTimeSlot = GetNextTimeSlot(nextTimeSlot);
            numSlots += 1;
        }
        return numSlots;
    }

    public static string GetCurrentTimeIndicatorString()
    {
        var today = DateTime.Now;
        var dayAbbreviation = today.ToString("ddd").ToLower();
        return dayAbbreviation + "-" + today.ToString("HH");
    }

}
