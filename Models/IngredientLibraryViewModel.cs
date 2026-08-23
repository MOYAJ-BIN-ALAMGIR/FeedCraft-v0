using FeedCraft.Domain.Models;

namespace FeedCraft.Web.Models;

/// <summary>
/// The Ingredient Library listing. Carries the nutrient definitions alongside the
/// ingredients because the table renders one column per nutrient.
/// </summary>
public class IngredientLibraryViewModel
{
    public List<Ingredient> Ingredients { get; set; } = new List<Ingredient>();

    public List<NutrientDefinition> Nutrients { get; set; } = new List<NutrientDefinition>();
}
