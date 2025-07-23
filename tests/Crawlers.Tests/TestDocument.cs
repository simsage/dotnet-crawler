namespace Crawlers.Tests;

public class TestDocument
{
    [Fact]
    public void TestDocumentFileExtension1()
    {
        Assert.Equal("xlsx", Document.GetFileExtension("https://demo.simsage.nz/assets/sample-spreadsheet.xlsx"));
    }
    
    [Fact]
    public void TestDocumentFileExtension2()
    {
        Assert.Equal("", Document.GetFileExtension("https://demo.simsage.nz/assets/sample-spreadsheet"));
    }
    
    [Fact]
    public void TestDocumentFileExtension3()
    {
        Assert.Equal("", Document.GetFileExtension("https://demo.simsage.nz"));
    }
    
    [Fact]
    public void TestDocumentFileExtension4()
    {
        Assert.Equal("pdf", Document.GetFileExtension("https://test.simsage.nz/test.pdf"));
    }
    
    [Fact]
    public void TestDocumentFileExtension5()
    {
        Assert.Equal("html", Document.GetFileExtension("\"https://dataset.simsage.co.uk/index.html?parameter=1.1&last=html#a1.1\""));
    }
    
}

