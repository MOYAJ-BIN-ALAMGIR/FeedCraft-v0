using System;
using System.Collections.Generic;
using System.Linq;

namespace FeedCraft.Domain.Models
{
    /// <summary>
    /// The difference between two solved formulations: what the change costs, and which
    /// ingredients moved to produce it.
    ///
    /// Everything here is numeric. Currency symbols and wording stay in the view, so the app
    /// still has exactly one place where money is formatted.
    /// </summary>
    public class FormulationDiff
    {
        /// <summary>False when at least one panel has no result to compare.</summary>
        public bool CanCompare { get; set; }

        /// <summary>Set only when <see cref="CanCompare"/> is false: which panel, and why.</summary>
        public string Message { get; set; } = string.Empty;

        public decimal CostA { get; set; }
        public decimal CostB { get; set; }

        public double BatchSizeA { get; set; }
        public double BatchSizeB { get; set; }

        /// <summary>Negative means panel B is cheaper.</summary>
        public decimal CostDelta => CostB - CostA;

        /// <summary>
        /// Batch sizes are edited per panel, so two totals can describe different amounts of
        /// feed. When this is true the totals are not comparable and only the per-kg figures
        /// mean anything — the view says so rather than quietly comparing them anyway.
        /// </summary>
        public bool BatchSizesDiffer => Math.Abs(BatchSizeA - BatchSizeB) > 0.0001;

        public decimal CostPerKgA => PerKg(CostA, BatchSizeA);
        public decimal CostPerKgB => PerKg(CostB, BatchSizeB);
        public decimal CostPerKgDelta => CostPerKgB - CostPerKgA;

        /// <summary>Change in cost per kg as a percentage. Zero when A costs nothing.</summary>
        public double CostDeltaPct => CostPerKgA == 0m
            ? 0.0
            : (double)(CostPerKgDelta / CostPerKgA) * 100.0;

        /// <summary>One row per ingredient in the union of both panels, biggest mover first.</summary>
        public List<IngredientMovement> Movements { get; set; } = new List<IngredientMovement>();

        public int ChangedCount => Movements.Count(m => m.Changed);

        /// <summary>The ingredient panel B leans on most heavily compared with A, if any.</summary>
        public IngredientMovement? BiggestIncrease =>
            Movements.Where(m => m.Changed && m.DeltaPct > 0).OrderByDescending(m => m.DeltaPct).FirstOrDefault();

        /// <summary>The ingredient panel B backs away from most, if any.</summary>
        public IngredientMovement? BiggestDecrease =>
            Movements.Where(m => m.Changed && m.DeltaPct < 0).OrderBy(m => m.DeltaPct).FirstOrDefault();

        private static decimal PerKg(decimal cost, double batchSize) =>
            batchSize <= 0 ? 0m : cost / (decimal)batchSize;
    }

    /// <summary>
    /// How much of one ingredient each panel used, as a percentage of its own batch.
    /// Percentages rather than kilograms, so the comparison survives different batch sizes.
    /// </summary>
    public class IngredientMovement
    {
        public int IngredientId { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>Null when this ingredient is not in that panel's list at all.</summary>
        public double? PercentA { get; set; }
        public double? PercentB { get; set; }

        public double DeltaPct => (PercentB ?? 0.0) - (PercentA ?? 0.0);

        public bool OnlyInA => PercentA.HasValue && !PercentB.HasValue;
        public bool OnlyInB => !PercentA.HasValue && PercentB.HasValue;

        /// <summary>
        /// Compares the values as *displayed* (two decimals) rather than the raw doubles, so a
        /// row can never be flagged as moved while showing two identical numbers — nor the
        /// reverse.
        /// </summary>
        public bool Changed =>
            OnlyInA || OnlyInB ||
            Math.Round(PercentA ?? 0.0, 2) != Math.Round(PercentB ?? 0.0, 2);
    }
}
