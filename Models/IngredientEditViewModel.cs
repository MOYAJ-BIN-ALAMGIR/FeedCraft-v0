using FeedCraft.Domain.Models;

namespace FeedCraft.Web.Models;

/// <summary>
/// Add/edit form for one library ingredient. <see cref="Nutrients"/> supplies the
/// labels and units for the per-nutrient inputs and is repopulated from the database
/// on every render — it is never bound back from the post.
/// </summary>
public class IngredientEditViewModel
{
    public Ingredient Ingredient { get; set; } = new Ingredient();

    public List<NutrientDefinition> Nutrients { get; set; } = new List<NutrientDefinition>();

    /// <summary>True on the edit screen, false when creating. Drives the heading and button text.</summary>
    public bool IsEdit { get; set; }
}
