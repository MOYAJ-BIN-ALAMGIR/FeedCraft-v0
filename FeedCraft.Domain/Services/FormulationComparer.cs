using System;
using System.Collections.Generic;
using System.Linq;
using FeedCraft.Domain.Models;

namespace FeedCraft.Domain.Services
{
    public class FormulationComparer : IFormulationComparer
    {
        public FormulationDiff Compare(FeedFormulationViewModel panelA, FeedFormulationViewModel panelB)
        {
            var diff = new FormulationDiff
            {
                CostA = panelA.TotalCost,
                CostB = panelB.TotalCost,
                BatchSizeA = panelA.BatchSize,
                BatchSizeB = panelB.BatchSize
            };

            if (!panelA.IsSolved || !panelB.IsSolved)
            {
                // A table of zeros would look like a real comparison in which nothing moved.
                // Say which panel has no answer, and repeat the solver's own reason for it.
                var reasons = new List<string>();
                if (!panelA.IsSolved) reasons.Add(Describe("A", panelA));
                if (!panelB.IsSolved) reasons.Add(Describe("B", panelB));

                diff.CanCompare = false;
                diff.Message = string.Join(" ", reasons);
                return diff;
            }

            diff.CanCompare = true;

            // Matched by Id over the union of both panels. The panels are independent, so the
            // user can add an ingredient to one and not the other — an unmatched ingredient has
            // to read as "only in A", never as a silent 0%.
            var ids = panelA.Ingredients.Select(i => i.Id)
                .Concat(panelB.Ingredients.Select(i => i.Id))
                .Distinct()
                .ToList();

            foreach (var id in ids)
            {
                var inA = panelA.FindIngredient(id);
                var inB = panelB.FindIngredient(id);

                diff.Movements.Add(new IngredientMovement
                {
                    IngredientId = id,
                    Name = NameFor(inA?.Name, inB?.Name),
                    PercentA = inA == null ? (double?)null : PercentOfBatch(panelA, id),
                    PercentB = inB == null ? (double?)null : PercentOfBatch(panelB, id)
                });
            }

            diff.Movements = diff.Movements
                .OrderByDescending(m => Math.Abs(m.DeltaPct))
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return diff;
        }

        /// <summary>
        /// Same arithmetic as the results table the user is looking at
        /// (quantity / batch size x 100), so the summary can never contradict the panel above it.
        /// An ingredient with no entry in the result dictionary was simply not used: 0%.
        /// </summary>
        private static double PercentOfBatch(FeedFormulationViewModel model, int ingredientId)
        {
            if (model.BatchSize <= 0) return 0.0;
            return model.OptimizedQuantities.TryGetValue(ingredientId, out var qty)
                ? qty / model.BatchSize * 100.0
                : 0.0;
        }

        /// <summary>
        /// Ingredient names are editable per panel, so the same Id can carry two labels. Showing
        /// both is more honest than picking one and leaving the user to wonder which panel it
        /// came from.
        /// </summary>
        private static string NameFor(string? nameInA, string? nameInB)
        {
            if (string.IsNullOrWhiteSpace(nameInA)) return nameInB ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nameInB)) return nameInA;
            return string.Equals(nameInA, nameInB, StringComparison.Ordinal)
                ? nameInA
                : $"{nameInA} / {nameInB}";
        }

        private static string Describe(string label, FeedFormulationViewModel model) =>
            string.IsNullOrWhiteSpace(model.ErrorMessage)
                ? $"Panel {label} has no result yet."
                : $"Panel {label} could not be solved: {model.ErrorMessage}";
    }
}
