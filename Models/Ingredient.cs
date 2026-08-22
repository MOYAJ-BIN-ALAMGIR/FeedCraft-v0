using System.ComponentModel.DataAnnotations;

namespace FeedCraft_v0.Models
{
    public class Ingredient
    {
        public string Name { get; set; } = string.Empty;
        public decimal CostPerUnit { get; set; }

        // Nutritional values (percentages usually, except ME which is often kcal/kg)
        public double CrudeProteinPct { get; set; }
        public double FatPct { get; set; }
        public double LysinePct { get; set; }
        public double AshPct { get; set; }
        public double ME { get; set; } // Metabolizable Energy (kcal/kg)

        // Per-ingredient inclusion limits, as a % of the batch.
        // Null Min is treated as 0%; null Max is treated as 100% (no cap).
        [Range(0, 100, ErrorMessage = "Min inclusion % must be between 0 and 100.")]
        public double? MinInclusionPct { get; set; }

        [Range(0, 100, ErrorMessage = "Max inclusion % must be between 0 and 100.")]
        public double? MaxInclusionPct { get; set; } = 100;
    }
}
