using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace FeedCraft.Domain.Models
{
    public class Ingredient
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Ingredient name is required.")]
        public string Name { get; set; } = string.Empty;

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
