using System.Collections.Generic;

namespace FeedCraft_v0.Models
{
    public class FeedFormulationViewModel
    {
        public List<Ingredient> Ingredients { get; set; } = new List<Ingredient>();
        public List<NutrientConstraint> Constraints { get; set; } = new List<NutrientConstraint>();
        public double BatchSize { get; set; } = 1000.0; // Default 1000 kg

        // Results
        public bool IsSolved { get; set; }
        public decimal TotalCost { get; set; }
        public Dictionary<string, double> OptimizedQuantities { get; set; } = new Dictionary<string, double>();
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
