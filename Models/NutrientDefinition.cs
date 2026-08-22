using System.ComponentModel.DataAnnotations;

namespace FeedCraft_v0.Models
{
    /// <summary>
    /// Defines a nutrient that the formulation can track and constrain.
    /// Nutrients are data, not code — adding one requires no C# changes.
    /// </summary>
    public class NutrientDefinition
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nutrient name is required.")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Display unit, e.g. "%" or "kcal/kg".</summary>
        public string Unit { get; set; } = "%";

        /// <summary>
        /// True when the value is a percentage of mass (CP, Fat, Lysine, Ash...).
        /// False for absolute per-unit measures such as ME in kcal/kg.
        /// The solver scales constraint bounds from this flag rather than from the name.
        /// </summary>
        public bool IsPercentage { get; set; } = true;
    }
}
