namespace Crawlers.Tests;

public class TestConvertAcls
{
    [Fact]
    public void TestConvertAcls1()
    {
        var aceList = new List<AccessControlEntry>();
        aceList.Add(createAce("SIMSAGE\\rock", "Domain", true));
        aceList.Add(createAce("Users", "Domain", true));

        var groupMap = createGroupMap();
        var userMap = createUserMap();
        
        var acls = MicrosoftFileShareCrawler.ConvertAcls(aceList, userMap, groupMap);
        Assert.Equal(2, acls.Count);
        
        Assert.Equal("rock@simsage.ai", acls[0].Name);
        Assert.Equal("Rock de Vocht", acls[0].DisplayName);
        Assert.True(acls[0].IsUser);
        
        Assert.Equal("Users", acls[1].Name);
        Assert.Single(acls[1].MembershipList);
        Assert.Contains("rock@simsage.ai", acls[1].MembershipList);
        Assert.False(acls[1].IsUser);
    }

    [Fact]
    public void TestConvertAcls2()
    {
        var aceList = new List<AccessControlEntry>();
        aceList.Add(createAce("SIMSAGE\\rock", "Domain", true));

        var groupMap = createGroupMap();
        var userMap = createUserMap();
        
        var acls = MicrosoftFileShareCrawler.ConvertAcls(aceList, userMap, groupMap);
        Assert.Single(acls);
        var acl0 = acls[0];
        Assert.Equal("rock@simsage.ai", acl0.Name);
        Assert.True(acl0.IsUser);
        Assert.Equal("Rock de Vocht", acl0.DisplayName);
    }

    [Fact]
    public void TestConvertAcls3()
    {
        var aceList = new List<AccessControlEntry>();
        // invalid ACL - a well-known type that isn't part of our normal list
        aceList.Add(createAce("SIMSAGE\\rock", "Well-Known", true));

        var groupMap = createGroupMap();
        var userMap = createUserMap();
        
        var acls = MicrosoftFileShareCrawler.ConvertAcls(aceList, userMap, groupMap);
        Assert.Empty(acls);
    }

    [Fact]
    public void TestConvertAcls4()
    {
        var aceList = new List<AccessControlEntry>();
        // invalid ACL - not allowed access - for now we ignore this ACL
        aceList.Add(createAce("SIMSAGE\\rock", "Domain", false));

        var groupMap = createGroupMap();
        var userMap = createUserMap();
        
        var acls = MicrosoftFileShareCrawler.ConvertAcls(aceList, userMap, groupMap);
        Assert.Empty(acls);
    }

    ///////////////////////////////////////////////////////////////////////////
    /// helpers

    private Dictionary<string, LdapGroup> createGroupMap()
    {
        var groupList = new List<LdapGroup>();
        groupList.Add(createGroup("Users", "Users", "", ["rock@simsage.ai"]));
        groupList.Add(createGroup("B", "B", "Berta", ["rock@simsage.ai"]));
        groupList.Add(createGroup("C", "C", "", ["nagendra@simsage.ai"]));
        var groupDict = new Dictionary<string, LdapGroup>();
        foreach (var group in groupList)
        {
            groupDict[group.Identity] = group;
        } 

        return groupDict;
    }

    private Dictionary<string, LdapUser> createUserMap()
    {
        var userList = new List<LdapUser>();
        userList.Add(createUser("simsage\\Rock", "Rock", "Rock de Vocht", "rock@simsage.ai"));
        userList.Add(createUser("SImSage\\nags", "nagendra", "", "nagendra@simsage.ai"));
        var userDict = new Dictionary<string, LdapUser>();
        foreach (var user in userList)
        {
            userDict[user.Identity] = user;
        }
        return userDict;
    }
    
    private AccessControlEntry createAce(string identity, string typeStr, bool allowAccess)
    {
        var ace = new AccessControlEntry();
        ace.Identity = identity;
        ace.Type = typeStr;
        ace.AccessControlType = allowAccess ? "Allow" : "Deny";
        return ace;
    }
   
    private LdapGroup createGroup(string identity, string samAccountName, string displayName, List<string> members)
    {
        var group = new LdapGroup();
        group.SamAccountName = samAccountName;
        group.Identity = identity.ToLower();
        group.DisplayName = displayName;
        group.Members = members;
        return group;
    }
   
    private LdapUser createUser(string identity, string samAccountName, string displayName, string email)
    {
        var user = new LdapUser();
        user.Email = email;
        user.Identity = identity.ToLower();
        user.SamAccountName = samAccountName;
        user.DisplayName = displayName;
        return user;
    }

}
