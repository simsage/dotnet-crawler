namespace Crawlers.Tests;

public class TestSource
{
    [Fact]
    public void TestSetSpecificJsonProperty1()
    {
        var source = new Source
        {
            SourceId = 123456789,
            Name = "Test",
            IsExternal = true,
            Acls = [new DocumentAcl("test", true)],
            InventoryOnlyMimeTypes = ["text/plain", "text/html"],
            SpecificJson = $"{{\"password\":\"1234\",\"api_token\":\"{Source.CT_FILE}\"}}"
        };

        var map1 = source.GetCrawlerPropertyMap();
        Assert.Equal("1234", map1["password"].ToString()); 
        Assert.Equal(Source.CT_FILE, map1["api_token"].ToString());

        source.SetSpecificJsonProperty("password", "5432");
        var map2 = source.GetCrawlerPropertyMap();
        Assert.Equal("5432", map2["password"].ToString());

        Assert.Equal("5432", source.SpecificJsonProperty("password"));
        Assert.Null(source.SpecificJsonProperty("not-set"));
        Assert.Null(source.SpecificJsonProperty(""));
    }
    
    [Fact]
    public void TestSetSpecificJsonProperty2()
    {
        var source = new Source
        {
            SourceId = 123456789,
            Name = "Test",
            IsExternal = true,
            Acls = [new DocumentAcl("test", true)],
            InventoryOnlyMimeTypes = ["text/plain", "text/html"],
            SpecificJson = ""
        };
        Assert.Null(source.SpecificJsonProperty("password"));

        source.SetSpecificJsonProperty("password", "5432");
        var map2 = source.GetCrawlerPropertyMap();
        Assert.Equal("5432", source.SpecificJsonProperty("password"));
    }

    
}