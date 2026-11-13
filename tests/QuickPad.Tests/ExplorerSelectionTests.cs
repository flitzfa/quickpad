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
}
