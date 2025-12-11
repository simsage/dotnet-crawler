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
    
    [Fact]
    public void TestDocumentFileExtension6()
    {
        Assert.Equal("docx", Document.GetFileExtension("\\\\192.168.2.17\\share1\\rock-test\\BOT NET CRAWLER-01994c70-e359-cfc3-683b-2e17aae79ecc-TEST-1985eed-fc75-Oam8Q~HHRvy~FYim.oyNUZPq16onjeD_bece-632c-2425efafdb6eDocument!@#$_&+_)(&^_$123456-7890,}{][`~c276f883-e0c8-43ae-9119-df8b7df9c574-test.docx"));
    }
    
}

