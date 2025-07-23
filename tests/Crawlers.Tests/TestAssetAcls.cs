namespace Crawlers.Tests;

public class TestAssetAcls
{
    [Fact]
    public void TestAssetAcls1()
    {
        List<AssetAcl> assetList = [
            new AssetAcl("test@simsage.ai", "Test User", "R"), 
            new AssetAcl("test@simsage.ai", "Test User", "R")
        ];
        Assert.Single(AssetAcl.UniqueAcls(assetList));
    }

    [Fact]
    public void TestAssetAcls2()
    {
        List<AssetAcl> assetList = [
            new AssetAcl("test@simsage.ai", "Test User", "R"), 
            new AssetAcl("test@simsage.ai", "Test User", "RW")
        ];
        Assert.Equal(2, AssetAcl.UniqueAcls(assetList).Count);
    }

}
