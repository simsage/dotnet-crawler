using System.Linq.Expressions;

namespace Crawlers.Tests;

public class TestResolveGroups
{
    [Fact]
    public void TestResolveGroups1()
    {
        var reader = new LdapReader("adPath", false, "username", "password");

        var groupList = new List<LdapGroup>();
        groupList.Add(createGroup("A", ["B", "rock@simsage.ai"]));
        groupList.Add(createGroup("B", ["C", "rock@simsage.ai"]));
        groupList.Add(createGroup("C", ["nagendra@simsage.ai"]));
        var groupDict = new Dictionary<string, LdapGroup>();
        foreach (var group in groupList)
        {
            groupDict[group.Identity] = group;
        }

        var userList = new List<LdapUser>();
        userList.Add(createUser("rock@simsage.ai"));
        userList.Add(createUser("nagendra@simsage.ai"));
        var userDict = new Dictionary<string, LdapUser>();
        foreach (var user in userList)
        {
            userDict[user.Identity] = user;
        }

        reader.ResolveGroups(groupList, userDict, groupDict);

        Assert.Equal(2, groupList[0].Members.Count);
        Assert.Contains("rock@simsage.ai", groupList[0].Members);
        Assert.Contains("nagendra@simsage.ai", groupList[0].Members);

        Assert.Equal(2, groupList[1].Members.Count);
        Assert.Contains("rock@simsage.ai", groupList[0].Members);
        Assert.Contains("nagendra@simsage.ai", groupList[0].Members);

        Assert.Single(groupList[2].Members);
        Assert.Contains("nagendra@simsage.ai", groupList[0].Members);
    }



    ///////////////////////////////////////////////////////////////////////////
    /// helpers
    
    private LdapGroup createGroup(string name, List<string> members)
    {
        var group = new LdapGroup();
        group.DisplayName = name;
        group.DistinguishedName = name;
        group.Identity = name;
        group.Members = members;
        return group;
    }
   
    private LdapUser createUser(string email)
    {
        var user = new LdapUser();
        user.Email = email;
        user.Identity = email;
        return user;
    }
   
}