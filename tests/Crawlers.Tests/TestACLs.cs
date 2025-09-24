namespace Crawlers.Tests;

public class TestACLs
{
    [Fact]
    public void ACLTest()
    {
        var list = PlatformCrawlerCommonProxy.EncodeAclList([
                new AssetAcl
                {
                    Name = "rock@simsage.ai",
                    DisplayName = "Rock de Vocht",
                    Access = "R",
                    IsUser = true,
                    MembershipList = []
                },
                new AssetAcl
                {
                    Name = "Users",
                    DisplayName = "",
                    Access = "RW",
                    IsUser = false,
                    MembershipList = ["rock@simsage.ai"]
                }
            ]
        );
        Assert.Equal(2, list.Count);
        var l0 = list[0];
        Assert.Equal("rock@simsage.ai", l0.Name);
        Assert.Equal("Rock de Vocht", l0.DisplayName);
        Assert.Equal("R", l0.Access);
        Assert.Equal(true, l0.IsUser);
        Assert.Equal(0, l0.MembershipList.Count);

        var l1 = list[1];
        Assert.Equal("Users", l1.Name);
        Assert.Equal("", l1.DisplayName);
        Assert.Equal("RW", l1.Access);
        Assert.Equal(false, l1.IsUser);
        Assert.Equal(1, l1.MembershipList.Count);
        Assert.True(l1.MembershipList.Contains("rock@simsage.ai"));
    }

}