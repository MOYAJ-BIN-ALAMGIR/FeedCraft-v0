using System.ComponentModel.DataAnnotations;

namespace FeedCraft.Domain.Models
{
    /// <summary>
    /// One nutrient reading for one ingredient — replaces the old fixed
    /// CrudeProteinPct / FatPct / LysinePct / AshPct / ME properties.
    /// </summary>
    public class IngredientNutrientValue
    {
        public int IngredientId { get; set; }
        public int NutrientDefinitionId { get; set; }

        /// <summary>
        /// Amount of the nutrient in this ingredient, expressed in the
        /// nutrient definition's unit (e.g. 44 for 44% CP, 3300 for 3300 kcal/kg).
        /// </summary>
        // An ingredient cannot contain a negative amount of anything. The upper bound is left open
        // because the unit varies with the nutrient — a % reading tops out near 100, an energy
        // reading in kcal/kg sits in the thousands.
        [Range(0, double.MaxValue, ErrorMessage = "A nutrient reading cannot be negative.")]
        public double Value { get; set; }
    }
}
