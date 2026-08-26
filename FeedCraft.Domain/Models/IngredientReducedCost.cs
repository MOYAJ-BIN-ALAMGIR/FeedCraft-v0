using System;

namespace FeedCraft.Domain.Models
{
    /// <summary>Why an ingredient is sitting on one of its inclusion limits instead of inside them.</summary>
    public enum InclusionVerdict
    {
        /// <summary>Not bought at all: at this price it cannot earn a place in the mix.</summary>
        PricedOut,

        /// <summary>Bought only because a minimum inclusion forces it in, at a premium.</summary>
        HeldInByMinimum,

        /// <summary>The optimizer wants more of it and a maximum inclusion is stopping it.</summary>
        CappedByMaximum
    }

    /// <summary>
    /// Why one ingredient did not make the mix — the reduced cost of its variable in the solved
    /// linear program, read as money.
    ///
    /// <para>A reduced cost answers "how far off is this ingredient's price?". For an ingredient
    /// the optimizer refused to buy it is the gap between what it costs and what it is worth at
    /// the margin, so subtracting it from the current price gives the price at which the answer
    /// would change. For one pinned against a maximum the sign flips meaning: the optimizer wanted
    /// more, and the number is what each extra kilogram would have saved.</para>
    ///
    /// <para>Only produced for ingredients whose reduced cost is non-zero, which is exactly those
    /// resting on an inclusion limit. An ingredient in the interior of the mix has a reduced cost
    /// of zero — it is priced at what it is worth here — and there is nothing to report about it.
    /// Meaningful only because the solver is an LP; see the note at step 3b of
    /// <c>FeedOptimizationService</c>.</para>
    /// </summary>
    public class IngredientReducedCost
    {
        public int IngredientId { get; set; }

        public InclusionVerdict Verdict { get; set; }

        /// <summary>
        /// The raw reduced cost straight from the solver, sign included, in $ per kg. Kept
        /// unmodified so the solver's sign convention stays inspectable; the figures below use
        /// its magnitude and take direction from <see cref="Verdict"/> instead.
        /// </summary>
        public double ReducedCostValue { get; set; }

        /// <summary>The ingredient's current price, $ per kg as entered.</summary>
        public decimal CostPerUnit { get; set; }

        /// <summary>
        /// The magnitude of the reduced cost, $ per kg: how much too expensive the ingredient is
        /// for <see cref="InclusionVerdict.PricedOut"/> and
        /// <see cref="InclusionVerdict.HeldInByMinimum"/>, and what each additional kilogram would
        /// save for <see cref="InclusionVerdict.CappedByMaximum"/>.
        /// </summary>
        public decimal PerKg { get; set; }

        /// <summary>
        /// The price at which this ingredient would start to earn a place, $ per kg. A prediction
        /// from the current basis, so it holds for a price that moves to roughly here — not for
        /// one that overshoots.
        /// </summary>
        public decimal BreakEvenCost { get; set; }

        /// <summary>
        /// False when <see cref="BreakEvenCost"/> comes out at or below zero: the ingredient is
        /// not merely dear, it would not earn a place even free, because of what it does to the
        /// binding targets. Saying "it would need to fall to -$0.12/kg" instead would be nonsense.
        /// </summary>
        public bool CanEverPayOff { get; set; }

        /// <summary>Kilograms of it in the solved batch — zero for a priced-out ingredient.</summary>
        public double Quantity { get; set; }

        public static IngredientReducedCost From(Ingredient ingredient, InclusionVerdict verdict,
            double reducedCost, double quantity)
        {
            // Magnitude only. Which direction the price is wrong in is already carried by the
            // limit the ingredient is resting on, which keeps this correct whichever sign
            // convention the solver build uses — the same reasoning as NutrientShadowPrice.From.
            decimal perKg = (decimal)Math.Abs(reducedCost);
            decimal breakEven = ingredient.CostPerUnit - perKg;

            return new IngredientReducedCost
            {
                IngredientId = ingredient.Id,
                Verdict = verdict,
                ReducedCostValue = reducedCost,
                CostPerUnit = ingredient.CostPerUnit,
                PerKg = perKg,
                BreakEvenCost = breakEven,
                CanEverPayOff = breakEven > 0m,
                Quantity = quantity
            };
        }
    }
}
