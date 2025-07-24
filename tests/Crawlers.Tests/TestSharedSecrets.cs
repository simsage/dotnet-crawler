namespace Crawlers.Tests;

public class TestSharedSecrets
{
    // AES key
    private const string OldAesKey = "199b7b02-4acb-4746-8399-50a72acfe124";

    [Fact]
    public void TestSharedSecrets1()
    {
        var n0 = SharedSecrets.GetRandomGuid(OldAesKey, 0);
        var n1 = SharedSecrets.GetRandomGuid(OldAesKey, 0);
        Assert.Equal(n0, n1);
        // with the old AES key, offset 0 MUST generate this guid
        Assert.Equal("07a68590-a4e9-0244-3b34-4c8a56beb325", n1.ToString());
    }

    [Fact]
    public void TestSharedSecrets2()
    {
        var n0 = SharedSecrets.GetRandomGuid(OldAesKey, -100);
        var n1 = SharedSecrets.GetRandomGuid(OldAesKey, -100);
        Assert.Equal(n0, n1);
        // with the old AES key, offset 0 MUST generate this guid
        Assert.Equal("e2a997b3-7f2b-d401-63c1-644ed2e39064", n1.ToString());
    }

    [Fact]
    public void TestSharedSecrets3()
    {
        var n0 = SharedSecrets.GetRandomGuid(OldAesKey, Int32.MinValue);
        var n1 = SharedSecrets.GetRandomGuid(OldAesKey, Int32.MinValue);
        Assert.Equal(n0, n1);
        // with the old AES key, offset 0 MUST generate this guid
        Assert.Equal("07a68590-a4e9-0244-3b34-4c8a56beb325", n1.ToString());
    }

    [Fact]
    public void TestSharedSecrets4()
    {
        var n0 = SharedSecrets.GetRandomGuid(OldAesKey, Int32.MaxValue);
        var n1 = SharedSecrets.GetRandomGuid(OldAesKey, Int32.MaxValue);
        Assert.Equal(n0, n1);
        // with the old AES key, offset 0 MUST generate this guid
        Assert.Equal("4e3654e7-c341-c78d-eebc-75eacea4a4d3", n1.ToString());
    }

    
}
