namespace Crawlers.Tests;

public class TestFileUtils
{
    [Fact]
    public void TestFileUtils1()
    {
        FileUtils.ReadMimeTypeInformation();
        Assert.Equal("application/pdf", FileUtils.FileTypeToMimeType("pdf"));
    }

    [Fact]
    public void TestFileUtils2()
    {
        FileUtils.ReadMimeTypeInformation();
        Assert.Equal("application/postscript", FileUtils.FileTypeToMimeType("ps"));
    }


    [Fact]
    public void TestFileUtils3()
    {
        FileUtils.ReadMimeTypeInformation();
        Assert.Equal("application/postscript", FileUtils.FileTypeToMimeType("eps"));
    }

}
