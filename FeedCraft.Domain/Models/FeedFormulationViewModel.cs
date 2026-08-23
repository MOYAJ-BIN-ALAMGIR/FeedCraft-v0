using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace FeedCraft.Domain.Models
{
    public class FeedFormulationViewModel
    {
        public List<Ingredient> Ingredients { get; set; } = new List<Ingredient>();

        /// <summary>The nutrients this formulation tracks. Driven by data, not code.</summary>
        public List<NutrientDefinition> NutrientDefinitions { get; set; } = new List<NutrientDefinition>();

        public List<NutrientConstraint> Constraints { get; set; } = new List<NutrientConstraint>();

        [Range(0.01, double.MaxValue, ErrorMessage = "Batch size must be greater than 0.")]
        public double BatchSize { get; set; } = 1000.0; // Default 1000 kg

        // Results — keyed by Id rather than by name, so duplicate or renamed
        // labels can't collide or silently mismatch.
        public bool IsSolved { get; set; }
        public decimal TotalCost { get; set; }
        public Dictionary<int, double> OptimizedQuantities { get; set; } = new Dictionary<int, double>();
        public Dictionary<int, double> CalculatedNutrients { get; set; } = new Dictionary<int, double>();
        public string ErrorMessage { get; set; } = string.Empty;

        public NutrientDefinition? FindNutrient(int id) =>
            NutrientDefinitions.FirstOrDefault(n => n.Id == id);

        public Ingredient? FindIngredient(int id) =>
            Ingredients.FirstOrDefault(i => i.Id == id);

        public NutrientConstraint? FindConstraint(int nutrientDefinitionId) =>
            Constraints.FirstOrDefault(c => c.NutrientDefinitionId == nutrientDefinitionId);

        /// <summary>
        /// Repairs relationships after model binding: assigns Ids to any rows the
        /// client added without one, drops nutrient values pointing at deleted
        /// definitions, and ensures every ingredient has a slot for every nutrient.
        /// </summary>
        public void Normalize()
        {
            NutrientDefinitions ??= new List<NutrientDefinition>();
            Ingredients ??= new List<Ingredient>();
            Constraints ??= new List<NutrientConstraint>();

            int nextNutrientId = NutrientDefinitions.Count == 0 ? 1 : NutrientDefinitions.Max(n => n.Id) + 1;
            foreach (var nutrient in NutrientDefinitions.Where(n => n.Id <= 0))
            {
                nutrient.Id = nextNutrientId++;
            }

            int nextIngredientId = Ingredients.Count == 0 ? 1 : Ingredients.Max(i => i.Id) + 1;
            foreach (var ingredient in Ingredients.Where(i => i.Id <= 0))
            {
                ingredient.Id = nextIngredientId++;
            }

            var validNutrientIds = NutrientDefinitions.Select(n => n.Id).ToHashSet();

            foreach (var ingredient in Ingredients)
            {
                ingredient.NutrientValues ??= new List<IngredientNutrientValue>();
                ingredient.NutrientValues.RemoveAll(v => !validNutrientIds.Contains(v.NutrientDefinitionId));

                foreach (var nutrient in NutrientDefinitions)
                {
                    var existing = ingredient.NutrientValues
                        .FirstOrDefault(v => v.NutrientDefinitionId == nutrient.Id);

                    if (existing == null)
                    {
                        ingredient.NutrientValues.Add(new IngredientNutrientValue
                        {
                            IngredientId = ingredient.Id,
                            NutrientDefinitionId = nutrient.Id,
                            Value = 0.0
                        });
                    }
                    else
                    {
                        existing.IngredientId = ingredient.Id;
                    }
                }

                // Keep display order aligned with the nutrient definition order.
                var order = NutrientDefinitions.Select((n, index) => new { n.Id, index })
                                               .ToDictionary(x => x.Id, x => x.index);
                ingredient.NutrientValues = ingredient.NutrientValues
                    .OrderBy(v => order.TryGetValue(v.NutrientDefinitionId, out var i) ? i : int.MaxValue)
                    .ToList();
            }

            // One constraint row per nutrient; drop orphans left by a deleted nutrient.
            Constraints.RemoveAll(c => !validNutrientIds.Contains(c.NutrientDefinitionId));
            foreach (var nutrient in NutrientDefinitions)
            {
                if (!Constraints.Any(c => c.NutrientDefinitionId == nutrient.Id))
                {
                    Constraints.Add(new NutrientConstraint { NutrientDefinitionId = nutrient.Id });
                }
            }
        }
    }
}
