using QuickPad.UI;

namespace QuickPad.Tests;

public class TextSearchTests
{
    [Fact] public void FindNext_FindsAfterFrom() => Assert.Equal(4, TextSearch.FindNext("a b a", "a", 1));
    [Fact] public void FindNext_WrapsAround() => Assert.Equal(0, TextSearch.FindNext("a b a", "a", 5));
    [Fact] public void FindNext_Backwards() => Assert.Equal(0, TextSearch.FindNext("a b a", "a", 4, true));
    [Fact] public void FindNext_BackwardsWrapsAround() => Assert.Equal(4, TextSearch.FindNext("a b a", "a", 0, true));
    [Fact] public void FindNext_IgnoresCase() => Assert.Equal(0, TextSearch.FindNext("Key", "kEY", 0));
    [Fact] public void FindNext_NoMatch() => Assert.Equal(-1, TextSearch.FindNext("a b a", "z", 0));
    [Fact] public void FindNext_EmptyQuery() => Assert.Equal(-1, TextSearch.FindNext("a b a", "", 0));
}
