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
    }
}
