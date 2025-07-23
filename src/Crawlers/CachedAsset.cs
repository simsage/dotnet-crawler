namespace Crawlers;

using System;
using System.ComponentModel.DataAnnotations;

public class CachedAsset
{
    private string _value = "";
    private readonly string _key = "";

    [Key] // Primary Key
    public string Key
    {
        get => _key;
        init
        {
            if (value.Length > 8192)
                throw new ArgumentException("Key length must be less than 8192 characters.");
            _key = value;
        }
    }

    [Required]
    public string Value
    {
        get => _value;
        set
        {
            if (value.Length > 8192)
                throw new ArgumentException("Value length must be less than 8192 characters.");
            _value = value;
        }
    }

    public long ExpiresAt { get; set; } // When does it expire? (for TTL)

    // Constructor for convenience (optional)
    public CachedAsset(string key, string value, long lifeSpanInMilliseconds)
    {
        Key = key;
        Value = value;
        ExpiresAt = DateTimeOffset.UtcNow.Add(TimeSpan.FromMilliseconds(lifeSpanInMilliseconds)).ToUnixTimeMilliseconds();
    }

    // Parameterless constructor for EF Core
    public CachedAsset() { }
}
