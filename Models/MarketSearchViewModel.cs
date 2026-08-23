using FeedCraft.Domain.Models;

namespace FeedCraft.Web.Models;

/// <summary>
/// The public market search. Results are every matching dealer's listing, cheapest first.
/// </summary>
public class MarketSearchViewModel
{
    public string? Query { get; set; }

    public List<DealerListing> Results { get; set; } = new List<DealerListing>();

    /// <summary>
    /// True when the visitor actually typed a search term. Only used to pick the wording of the
    /// empty state — "nothing matches 'Corn'" reads very differently from "no dealers have
    /// listed anything yet".
    /// </summary>
    public bool HasQuery { get; set; }
}
