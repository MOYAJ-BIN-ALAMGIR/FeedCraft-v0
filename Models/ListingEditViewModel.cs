using FeedCraft.Domain.Models;

namespace FeedCraft.Web.Models;

/// <summary>
/// Editor model for one dealer listing, shaped like <see cref="IngredientEditViewModel"/> so the
/// Create and Edit views can share a single form partial.
///
/// The bound <see cref="Listing"/> carries a DealerUserId property, but ListingsController never
/// reads it from the post — the owner is taken from the signed-in principal instead.
/// </summary>
public class ListingEditViewModel
{
    public DealerListing Listing { get; set; } = new DealerListing();

    public bool IsEdit { get; set; }
}
