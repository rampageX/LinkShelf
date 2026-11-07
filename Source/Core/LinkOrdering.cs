namespace LinkShelf.Core;

public static class LinkOrdering
{
    // Keep the user's existing column order inside each group. The stable
    // partition only pins newly discovered links above the old links.
    public static List<LinkRow> NewFirst(IEnumerable<LinkRow> alreadySorted, IReadOnlySet<string> newPaths) =>
        alreadySorted.OrderByDescending(row => newPaths.Contains(row.Path)).ToList();
}
