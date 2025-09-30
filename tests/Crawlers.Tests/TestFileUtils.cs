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

    [Fact]
    public void TestFileUtils4()
    {
        FileUtils.ReadMimeTypeInformation();
        Assert.Equal("application/pdf", FileUtils.FileTypeToMimeType("pdf"));
        Assert.Equal(50 * 1024L * 1024L, FileUtils.MaximumSizeInBytesForMimeType("application/pdf"));
        Assert.True(FileUtils.IsValidMimeType("application/pdf"));
    }

    [Fact]
    public void TestFileUtils5()
    {
        FileUtils.ReadMimeTypeInformation();
        Assert.Equal("application/postscript", FileUtils.FileTypeToMimeType("ps"));
        Assert.Equal(50 * 1024L * 1024L, FileUtils.MaximumSizeInBytesForMimeType("application/postscript"));
        Assert.True(FileUtils.IsValidMimeType("application/postscript"));
    }


    [Fact]
    public void TestFileUtils6()
    {
        FileUtils.ReadMimeTypeInformation();
        Assert.Equal("application/vnd.isac.fcs", FileUtils.FileTypeToMimeType("fcs"));
        Assert.Equal(1 * 1024L * 1024L, FileUtils.MaximumSizeInBytesForMimeType("application/vnd.isac.fcs"));
        Assert.False(FileUtils.IsValidMimeType("application/vnd.isac.fcs"));
    }

}
