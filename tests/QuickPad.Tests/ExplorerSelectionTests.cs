using QuickPad.Shell;

namespace QuickPad.Tests;

public class ExplorerSelectionTests
{
    [Fact]
    public async Task Returns_null_for_a_window_that_is_not_Explorer()
    {
        Assert.Null(await ExplorerSelection.GetAsync(IntPtr.Zero));
        Assert.Null(await ExplorerSelection.GetAsync(new IntPtr(0x7FFF_FFF0))); // not a window handle
    }

    static readonly (string Name, string Path, bool IsFolder)[] Folder =
    [
        ("config", @"C:\p\config.env", false),
        ("config", @"C:\p\config.txt", false), // same display name with extensions hidden
        ("notas.txt", @"C:\p\notas.txt", false),
        ("src", @"C:\p\src", true),
    ];

    [Fact]
    public void MatchByName_UniqueFile_ReturnsItsPath()
    {
        Assert.Equal(@"C:\p\notas.txt", ExplorerSelection.MatchByName("notas.txt", Folder, out bool folder));
        Assert.False(folder);
    }

    [Fact]
    public void MatchByName_Folder_ReturnsNullAndFlagsIt()
    {
        Assert.Null(ExplorerSelection.MatchByName("src", Folder, out bool folder));
        Assert.True(folder);
    }

    [Theory]
    [InlineData("config")]   // ambiguous: don't guess
    [InlineData("otro.txt")] // not there
    [InlineData("NOTAS.TXT")] // display names are compared exactly
    public void MatchByName_AmbiguousOrMissing_ReturnsNull(string name)
    {
        Assert.Null(ExplorerSelection.MatchByName(name, Folder, out bool folder));
        Assert.False(folder);
    }
}
