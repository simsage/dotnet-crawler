namespace Crawlers.Tests;

public class TestAcls
{
    [Fact]
    public void AclTest()
    {
        var list = new List<AssetAcl>([
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
        Assert.True(l0.IsUser);
        Assert.Empty(l0.MembershipList);

        var l1 = list[1];
        Assert.Equal("Users", l1.Name);
        Assert.Equal("", l1.DisplayName);
        Assert.Equal("RW", l1.Access);
        Assert.False(l1.IsUser);
        Assert.Single(l1.MembershipList);
        Assert.Contains("rock@simsage.ai", l1.MembershipList);
    }

}