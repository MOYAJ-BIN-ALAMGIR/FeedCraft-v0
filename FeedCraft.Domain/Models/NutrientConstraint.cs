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
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
    }
}
