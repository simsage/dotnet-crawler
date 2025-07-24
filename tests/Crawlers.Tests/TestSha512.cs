namespace Crawlers.Tests;

public class TestSha512
{
    // AES key
    private const string OldAesKey = "345251e3-c7ef-4d71-8afd-84b89345f148";

    [Fact]
    public void TestSharedSecrets1()
    {
        var resultStr = Sha512.GenerateSha512Hash(OldAesKey, "Password1");
        Assert.Equal(128, resultStr.Trim().Length);
        Assert.Equal(
            "8a812749fbf5fccb54464dfb4c654d1862a8ad6181ccd5e27480fed0ac42dbe8d31f18156abde12bbbbb876d57f2a61a51580dcfe9f03e47d35492050b3cc6d6",
            resultStr
            );
    }

}
