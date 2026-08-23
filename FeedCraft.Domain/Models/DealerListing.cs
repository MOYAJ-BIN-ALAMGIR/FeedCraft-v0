using System;
using System.ComponentModel.DataAnnotations;

namespace FeedCraft.Domain.Models
{
    /// <summary>
    /// One dealer's standing offer to supply one ingredient at a price.
    ///
    /// Note what is deliberately absent: this class has no navigation property to an Identity
    /// user, and no reference to ASP.NET Core Identity at all. <see cref="DealerUserId"/> is a
    /// plain string, and the foreign key to AspNetUsers is declared in FeedCraftDbContext where
    /// IdentityUser is already in scope. That is what lets the Domain layer stay ignorant of how
    /// — or whether — users are authenticated.
    /// </summary>
    public class DealerListing
    {
        public int Id { get; set; }

        /// <summary>
        /// The owning dealer's Identity user id. Always assigned server-side from the signed-in
        /// principal and never bound from a form: if this were postable, a dealer could publish a
        /// listing attributed to somebody else.
        /// </summary>
        public string DealerUserId { get; set; } = string.Empty;

        /// <summary>
        /// Free text rather than a foreign key to Ingredient. Dealers sell things the local
        /// library may not have a row for yet, and a listing should not require an admin to
        /// create the ingredient first. The Market page matches on this with a LIKE.
        /// </summary>
        [Required(ErrorMessage = "Ingredient name is required.")]
        [StringLength(100, ErrorMessage = "Ingredient name must be 100 characters or fewer.")]
        [Display(Name = "Ingredient")]
        public string IngredientName { get; set; } = string.Empty;

        [Range(0.0, 1000000.0, ErrorMessage = "Price must be 0 or greater.")]
        [Display(Name = "Price per kg")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Contact info is required — buyers need a way to reach you.")]
        [StringLength(300, ErrorMessage = "Contact info must be 300 characters or fewer.")]
        [Display(Name = "Contact info")]
        public string ContactInfo { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; }
    }
}
