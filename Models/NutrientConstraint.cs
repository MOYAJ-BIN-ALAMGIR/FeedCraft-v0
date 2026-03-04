namespace FeedCraft_v0.Models
{
    public class NutrientConstraint
    {
        public string NutrientName { get; set; } = string.Empty;
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
    }
}
