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
        public double Value { get; set; }
    }
}
