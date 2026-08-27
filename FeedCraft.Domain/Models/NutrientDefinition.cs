using System.ComponentModel.DataAnnotations;

namespace FeedCraft.Domain.Models
{
    /// <summary>
    /// Defines a nutrient that the formulation can track and constrain.
    /// Nutrients are data, not code — adding one requires no C# changes.
    /// </summary>
    public class NutrientDefinition
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nutrient name is required.")]
        [StringLength(100, ErrorMessage = "Nutrient name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Display unit, e.g. "%" or "kcal/kg".</summary>
        // Required because the unit is not decoration: it is read back into the sentences the
        // sensitivity table and the leaderboard build ("per 1%", "per 1 kcal/kg"). A blank unit
        // leaves those reading "per 1" with nothing after it.
        [Required(ErrorMessage = "Nutrient unit is required (for example % or kcal/kg).")]
        [StringLength(20, ErrorMessage = "Nutrient unit cannot exceed 20 characters.")]
        public string Unit { get; set; } = "%";

        /// <summary>
        /// True when the value is a percentage of mass (CP, Fat, Lysine, Ash...).
        /// False for absolute per-unit measures such as ME in kcal/kg.
        /// The solver scales constraint bounds from this flag rather than from the name.
        /// </summary>
        public bool IsPercentage { get; set; } = true;
    }
}
