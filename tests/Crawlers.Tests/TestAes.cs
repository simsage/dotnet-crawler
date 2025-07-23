namespace Crawlers.Tests;

using Xunit;
using Crawlers;

public class TestAes
{
    [Fact]
    public void DecryptTest()
    {
        var aes = new AesEncryption();
        var str = "Password1";
        var encryptedString = aes.Encrypt(str);
        var decryptedString = aes.Decrypt(encryptedString);
        Assert.Equal(str, decryptedString);
    }
    
}
