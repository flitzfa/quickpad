using QuickPad.Shell;

namespace QuickPad.Tests;

public class ExplorerSelectionTests
{
    [Fact]
    public void Returns_null_for_a_window_that_is_not_Explorer()
    {
        Assert.Null(ExplorerSelection.Get(IntPtr.Zero));
        Assert.Null(ExplorerSelection.Get(new IntPtr(0x7FFF_FFF0))); // not a window handle
    }
}
