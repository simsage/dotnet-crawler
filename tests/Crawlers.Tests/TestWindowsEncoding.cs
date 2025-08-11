using System.Text;

namespace Crawlers.Tests;

public class TestWindowsEncoding
{
    // check we encode a windows 1252 string to a utf-8 string on windows
    [Fact]
    public void EncodingTest1()
    {
        if (RockUtils.IsWindows())
        {
            byte[] windows1252Bytes = [0x66, 0x72, 0x61, 0x6e, 0xe7, 0x61, 0x69, 0x73, 0x20, 0xa9];
            var windowsStr = Encoding.GetEncoding("Windows-1252").GetString(windows1252Bytes);
            var utf8Str = RockUtils.Windows1252ToUtf8(windowsStr);
            Assert.False(windowsStr.Equals(utf8Str));
        }
    }
    
}

