using System.Collections.Specialized;
using WindowsTrayTranslator.Clipboard;

namespace WindowsTrayTranslator.Tests;

public sealed class ClipboardSnapshotTests
{
    [Fact]
    public void CloneSupportedValue_ClonesMutableStandardFormats()
    {
        string[] files = ["a.txt", "b.txt"];
        byte[] bytes = [1, 2, 3];
        StringCollection collection = ["a", "b"];

        string[] clonedFiles = Assert.IsType<string[]>(ClipboardService.CloneSupportedValue(files));
        byte[] clonedBytes = Assert.IsType<byte[]>(ClipboardService.CloneSupportedValue(bytes));
        StringCollection clonedCollection = Assert.IsType<StringCollection>(ClipboardService.CloneSupportedValue(collection));

        Assert.NotSame(files, clonedFiles);
        Assert.NotSame(bytes, clonedBytes);
        Assert.NotSame(collection, clonedCollection);
        Assert.Equal(files, clonedFiles);
        Assert.Equal(bytes, clonedBytes);
    }

    [Fact]
    public void CloneSupportedValue_UnsupportedReference_ReturnsNull()
    {
        Assert.Null(ClipboardService.CloneSupportedValue(new object()));
    }
}
