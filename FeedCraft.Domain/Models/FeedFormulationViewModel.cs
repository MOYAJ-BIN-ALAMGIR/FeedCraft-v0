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

        /// <summary>
        /// Label used when saving this formulation. Part of the form (rather than a loose
        /// parameter) so it survives a validation re-render, and so loading a saved
        /// formulation puts its name back in the box.
        /// </summary>
        [StringLength(200, ErrorMessage = "Formulation name must be 200 characters or fewer.")]
        public string? SaveName { get; set; }

        // Results — keyed by Id rather than by name, so duplicate or renamed
        // labels can't collide or silently mismatch.
        public bool IsSolved { get; set; }
        public decimal TotalCost { get; set; }
        public Dictionary<int, double> OptimizedQuantities { get; set; } = new Dictionary<int, double>();
        public Dictionary<int, double> CalculatedNutrients { get; set; } = new Dictionary<int, double>();

        /// <summary>
        /// The marginal cost of each <i>binding</i> nutrient target, keyed by nutrient Id.
        /// Targets with slack are absent rather than present-with-zero: a zero dual means the
        /// constraint is not driving the price, so there is nothing to report about it.
        /// </summary>
        public Dictionary<int, NutrientShadowPrice> ShadowPrices { get; set; } = new Dictionary<int, NutrientShadowPrice>();

        /// <summary>
        /// True once the solver has been asked for dual values. Distinguishes "analysed, and
        /// nothing is binding" from "never analysed" — an empty <see cref="ShadowPrices"/> means
        /// the first only if this is set. Snapshots saved before sensitivity analysis existed
        /// deserialize with this false, so they render no sensitivity section rather than
        /// claiming that none of their targets were binding.
        /// </summary>
        public bool SensitivityComputed { get; set; }

        /// <summary>
        /// Why an ingredient is stuck on an inclusion limit, keyed by ingredient Id. Ingredients
        /// the optimizer chose freely are absent rather than present-with-zero, for the same reason
        /// slack targets are absent from <see cref="ShadowPrices"/>: a zero reduced cost means the
        /// ingredient is worth what it costs here, so there is nothing to say about it.
        ///
        /// No companion "was this computed" flag, deliberately. The section that renders this shows
        /// nothing at all when the dictionary is empty, so a snapshot saved before reduced costs
        /// existed stays silent instead of claiming every ingredient was freely chosen.
        /// </summary>
        public Dictionary<int, IngredientReducedCost> ReducedCosts { get; set; } = new Dictionary<int, IngredientReducedCost>();

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

            // Re-sorted for the same reason the ingredient readings above are: the nutrients
            // table renders Constraints[k] beside NutrientDefinitions[k], so a row appended
            // out of order by the loop above would show its min/max against the wrong
            // nutrient's name. Callers that build a partial constraint list — loading a
            // knowledge-base template, for one — depend on this.
            var constraintOrder = NutrientDefinitions.Select((n, index) => new { n.Id, index })
                                                     .ToDictionary(x => x.Id, x => x.index);
            Constraints = Constraints
                .OrderBy(c => constraintOrder.TryGetValue(c.NutrientDefinitionId, out var i) ? i : int.MaxValue)
                .ToList();
        }
    }
}
