namespace Crawlers.Tests;

public class TestSharedSecrets
{
    // AES key
    private const string OldAesKey = "345251e3-c7ef-4d71-8afd-84b89345f148";

    [Fact]
    public void TestSharedSecrets1()
    {
        var n0 = SharedSecrets.GetRandomGuid(OldAesKey, 0);
        var n1 = SharedSecrets.GetRandomGuid(OldAesKey, 0);
        Assert.Equal(n0, n1);
        // with the old AES key, offset 0 MUST generate this guid
        Assert.Equal("2dd2fa10-0850-328b-3900-5065d1ad807c", n1.ToString());
    }

    [Fact]
    public void TestSharedSecrets2()
    {
        var n0 = SharedSecrets.GetRandomGuid(OldAesKey, -100);
        var n1 = SharedSecrets.GetRandomGuid(OldAesKey, -100);
        Assert.Equal(n0, n1);
        // with the old AES key, offset 0 MUST generate this guid
        Assert.Equal("432fb103-ea7c-332c-8b52-46b51a129aa1", n1.ToString());
    }

    [Fact]
    public void TestSharedSecrets3()
    {
        var n0 = SharedSecrets.GetRandomGuid(OldAesKey, Int32.MinValue);
        var n1 = SharedSecrets.GetRandomGuid(OldAesKey, Int32.MinValue);
        Assert.Equal(n0, n1);
        // with the old AES key, offset 0 MUST generate this guid
        Assert.Equal("2dd2fa10-0850-328b-3900-5065d1ad807c", n1.ToString());
    }

    [Fact]
    public void TestSharedSecrets4()
    {
        var n0 = SharedSecrets.GetRandomGuid(OldAesKey, Int32.MaxValue);
        var n1 = SharedSecrets.GetRandomGuid(OldAesKey, Int32.MaxValue);
        Assert.Equal(n0, n1);
        // with the old AES key, offset 0 MUST generate this guid
        Assert.Equal("7390bf87-95b9-9a9c-d000-e4e230329e94", n1.ToString());
    }

    
}
