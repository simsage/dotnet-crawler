namespace Crawlers.Tests;

public class TestAsset
{
    [Fact]
    public void TestAsset1()
    {
        var asset = new Asset();
        Assert.Equal([], asset.ReadBytes());
    }

    [Fact]
    public void TestAsset2()
    {
        var tempFilename = FileUtils.GetTempFilename();
        File.WriteAllBytes(tempFilename, [1, 2, 3, 4, 5]);
        Assert.True(File.Exists(tempFilename));
        
        var asset = new Asset();
        Assert.Equal([], asset.ReadBytes());
        asset.Filename = tempFilename;
        Assert.Equal([1, 2, 3, 4, 5], asset.ReadBytes());
        Assert.Equal([1, 2, 3, 4, 5], asset.ReadBytesAndRemoveFile());
        Assert.False(File.Exists(tempFilename));
    }

    [Fact]
    public void TestAsset3()
    {
        var tempFilename = FileUtils.GetTempFilename();
        File.WriteAllBytes(tempFilename, [1, 2, 3, 4, 5]);
        Assert.True(File.Exists(tempFilename));
        Assert.True(Asset.IsFile(new FileInfo(tempFilename)));
        File.Delete(tempFilename);
        Assert.False(File.Exists(tempFilename));
    }

    [Fact]
    public void TestAsset4()
    {
        var tempFilename = FileUtils.GetTempFilename();
        File.WriteAllBytes(tempFilename, [1, 2, 3, 4, 5]);
        Assert.True(File.Exists(tempFilename));

        var asset = new Asset
        {
            Filename = tempFilename
        };

        var hash1 = asset.CalculateHash();
        Assert.Equal("c6f2f829cf5861d76389bfd6831e28f6", hash1);
        
        asset.Acls = [new AssetAcl("test@simsage.ai", "Test User", "R")];
        var hash2 = asset.CalculateHash();
        Assert.Equal("c6f2f829cf5861d76389bfd6831e28f6", hash1);
        
        File.Delete(tempFilename);
        Assert.False(File.Exists(tempFilename));
    }

    
}
