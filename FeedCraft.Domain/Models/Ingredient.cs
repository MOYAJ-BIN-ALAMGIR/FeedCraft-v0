using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace FeedCraft.Domain.Models
{
    public class Ingredient
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Ingredient name is required.")]
        [StringLength(100, ErrorMessage = "Ingredient name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        // A cost of 0 is legitimate (a free byproduct); a negative one is not. Without this the
        // solver treats a negative price as a reward and buys as much of the ingredient as its
        // inclusion limit allows. Range's double overload validates a decimal property correctly —
        // same pattern as DealerListing.Price.
        [Range(0.0, 1000000.0, ErrorMessage = "Cost per unit must be 0 or greater.")]
        public decimal CostPerUnit { get; set; }

        /// <summary>
        /// Nutrient readings for this ingredient, one per NutrientDefinition.
        /// A definition with no entry here is treated as zero.
        /// </summary>
        public List<IngredientNutrientValue> NutrientValues { get; set; } = new List<IngredientNutrientValue>();

        // Per-ingredient inclusion limits, as a % of the batch.
        // Null Min is treated as 0%; null Max is treated as 100% (no cap).
        [Range(0, 100, ErrorMessage = "Min inclusion % must be between 0 and 100.")]
        public double? MinInclusionPct { get; set; }

        [Range(0, 100, ErrorMessage = "Max inclusion % must be between 0 and 100.")]
        public double? MaxInclusionPct { get; set; } = 100;

        /// <summary>
        /// Value for the given nutrient, or 0 when this ingredient has no reading for it.
        /// </summary>
        public double GetNutrientValue(int nutrientDefinitionId) =>
            NutrientValues.FirstOrDefault(v => v.NutrientDefinitionId == nutrientDefinitionId)?.Value ?? 0.0;
    }
}
