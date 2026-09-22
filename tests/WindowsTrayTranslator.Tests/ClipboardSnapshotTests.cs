using System.Collections.Specialized;
using System.Runtime.InteropServices;
using ComTypes = System.Runtime.InteropServices.ComTypes;
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

    [Fact]
    public void CloneSupportedValue_ComStream_CopiesBytesAndRestoresPosition()
    {
        FakeComStream source = new([1, 2, 3, 4]);
        source.Seek(2, 0, nint.Zero);

        using MemoryStream clone = Assert.IsType<MemoryStream>(ClipboardService.CloneSupportedValue(source));

        Assert.Equal([1, 2, 3, 4], clone.ToArray());
        Assert.Equal(2, source.Position);
    }

    private sealed class FakeComStream(byte[] contents) : ComTypes.IStream
    {
        private readonly MemoryStream stream = new(contents);

        public long Position => stream.Position;

        public void Read(byte[] pv, int cb, nint pcbRead)
        {
            int bytesRead = stream.Read(pv, 0, cb);
            if (pcbRead != nint.Zero)
            {
                Marshal.WriteInt32(pcbRead, bytesRead);
            }
        }

        public void Seek(long dlibMove, int dwOrigin, nint plibNewPosition)
        {
            long position = stream.Seek(dlibMove, (SeekOrigin)dwOrigin);
            if (plibNewPosition != nint.Zero)
            {
                Marshal.WriteInt64(plibNewPosition, position);
            }
        }

        public void Write(byte[] pv, int cb, nint pcbWritten) => throw new NotSupportedException();
        public void SetSize(long libNewSize) => throw new NotSupportedException();
        public void CopyTo(ComTypes.IStream pstm, long cb, nint pcbRead, nint pcbWritten) => throw new NotSupportedException();
        public void Commit(int grfCommitFlags) => throw new NotSupportedException();
        public void Revert() => throw new NotSupportedException();
        public void LockRegion(long libOffset, long cb, int dwLockType) => throw new NotSupportedException();
        public void UnlockRegion(long libOffset, long cb, int dwLockType) => throw new NotSupportedException();
        public void Stat(out ComTypes.STATSTG pstatstg, int grfStatFlag) => throw new NotSupportedException();
        public void Clone(out ComTypes.IStream ppstm) => throw new NotSupportedException();
    }
}
