namespace QuickPad.UI;

public static class TextSearch
{
    private const StringComparison Cmp = StringComparison.CurrentCultureIgnoreCase;

    /// <summary>Index of the first match at or after <paramref name="from"/> (or the last match starting before it
    /// when <paramref name="backwards"/>), wrapping around the text; -1 when there is none.</summary>
    public static int FindNext(string text, string query, int from, bool backwards = false)
    {
        if (query.Length == 0 || text.Length == 0) return -1;
        from = Math.Clamp(from, 0, text.Length);
        if (!backwards)
        {
            int i = text.IndexOf(query, from, Cmp);
            return i >= 0 ? i : text.IndexOf(query, Cmp);
        }
        // LastIndexOf only finds matches that end at or before startIndex, so this admits every start < from.
        int start = Math.Min(text.Length - 1, from + query.Length - 2);
        int j = start >= 0 ? text.LastIndexOf(query, start, Cmp) : -1;
        return j >= 0 ? j : text.LastIndexOf(query, Cmp);
    }
}
