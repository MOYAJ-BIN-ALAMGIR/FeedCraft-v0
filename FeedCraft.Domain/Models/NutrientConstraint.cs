using System.ComponentModel.DataAnnotations;

namespace FeedCraft.Domain.Models
{
    /// <summary>
    /// A min/max target for one nutrient. References a NutrientDefinition by Id
    /// rather than by free-text name, so a typo can no longer silently
    /// resolve to a nutrient value of zero.
    /// </summary>
    public class NutrientConstraint
    {
        public int NutrientDefinitionId { get; set; }

        // A negative target is meaningless for every nutrient the app tracks — percentages and
        // absolute measures alike. The upper bound stays open because units differ by orders of
        // magnitude (24 for CP%, 3200 for ME in kcal/kg).
        //
        // Both are nullable, and a blank field binds to null rather than 0; Range passes on null,
        // so "no target" is still expressed by leaving the box empty. Whether min exceeds max is a
        // relationship between two properties, so it stays a solver-side check.
        [Range(0, double.MaxValue, ErrorMessage = "Minimum value cannot be negative.")]
        public double? MinValue { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Maximum value cannot be negative.")]
        public double? MaxValue { get; set; }
    }
}
