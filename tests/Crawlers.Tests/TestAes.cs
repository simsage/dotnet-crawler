namespace Crawlers.Tests;

using Xunit;
using Crawlers;

public class TestAes
{
    // AES key
    private const string OldAesKey = "345251e3-c7ef-4d71-8afd-84b89345f148";

    [Fact]
    public void DecryptTest()
    {
        AesEncryption.DataAesKey = OldAesKey;
        var aes = new AesEncryption();
        var str = "Password1";
        var encryptedString = aes.Encrypt(str);
        var decryptedString = aes.Decrypt(encryptedString);
        Assert.Equal(str, decryptedString);
    }
    
}
