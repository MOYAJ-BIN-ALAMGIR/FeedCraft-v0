using System;

namespace FeedCraft.Domain.Models
{
    /// <summary>Which side of a nutrient target the optimum is pressed against.</summary>
    public enum BindingSide
    {
        Minimum,
        Maximum,

        /// <summary>Min and Max are the same value, so the level is pinned from both sides.</summary>
        Fixed
    }

    /// <summary>
    /// The marginal cost of one nutrient target — the dual value of its constraint in the
    /// solved linear program, translated into money the user can act on.
    ///
    /// Only produced for constraints that are actually binding. A target with slack has a dual
    /// of zero: the mix is not pressed against it, so moving it slightly changes nothing.
    ///
    /// <para><b>Units.</b> The solver's variables are kg and its objective is whole-batch cost,
    /// so a nutrient row is <c>Sum(value_i * x_i) in [min * BatchSize, max * BatchSize]</c> and its
    /// right-hand side is measured in <i>nutrient-unit x kg</i>. The dual is therefore
    /// <c>$ / (nutrient-unit * kg)</c>. Multiplying by 1000 kg substitutes a tonne for the batch,
    /// which is what makes every figure here per-tonne no matter what batch size was entered.</para>
    /// </summary>
    public class NutrientShadowPrice
    {
        /// <summary>Kilograms in a tonne — the basis every figure on this class is reported against.</summary>
        public const double KgPerTonne = 1000.0;

        public int NutrientDefinitionId { get; set; }

        public BindingSide Side { get; set; }

        /// <summary>The min or max the level is sitting on, in the nutrient's own unit.</summary>
        public double BoundValue { get; set; }

        /// <summary>
        /// The raw dual straight from the solver, sign included: <c>d(total cost) / d(RHS)</c> in
        /// $ per nutrient-unit per kg. Kept unmodified so the solver's sign convention stays
        /// inspectable; the display figures below use its magnitude.
        /// </summary>
        public double DualValue { get; set; }

        /// <summary>What one whole unit of this requirement costs, in $ per tonne of feed.</summary>
        public double CostPerTonnePerUnit { get; set; }

        /// <summary>
        /// The small step the plain-English interpretation is quoted against, in the nutrient's
        /// own unit. See <see cref="ChooseDelta"/> for why it is not a fixed number.
        /// </summary>
        public double Delta { get; set; }

        /// <summary>
        /// What relaxing this target by <see cref="Delta"/> is predicted to save, in $ per tonne.
        /// A prediction, not a promise: it is linear, and a dual only holds until the optimal
        /// basis changes.
        /// </summary>
        public decimal SavingPerTonne { get; set; }

        public static NutrientShadowPrice From(NutrientDefinition nutrient, BindingSide side,
            double boundValue, double dual)
        {
            // Only the magnitude is used. Relaxing a constraint can never make a minimisation
            // more expensive, so the direction of the saving is already carried by `side` —
            // which keeps this correct whichever sign convention the solver build uses.
            double rate = Math.Abs(dual);
            double delta = ChooseDelta(nutrient.IsPercentage, boundValue);

            return new NutrientShadowPrice
            {
                NutrientDefinitionId = nutrient.Id,
                Side = side,
                BoundValue = boundValue,
                DualValue = dual,
                CostPerTonnePerUnit = rate * KgPerTonne,
                Delta = delta,
                SavingPerTonne = (decimal)(rate * delta * KgPerTonne)
            };
        }

        /// <summary>
        /// Picks the step to quote the saving against: 1% of the target, rounded to one
        /// significant figure.
        ///
        /// <para>Scaled rather than fixed for two reasons. A single number cannot fit both units —
        /// 0.1 is a sensible nudge to a 1% lysine minimum and meaningless against a 2950 kcal/kg
        /// energy minimum. And a dual is only valid until the optimal basis changes, so quoting
        /// too large a step produces a confident overshoot: on the demo dataset, lysine's dual
        /// holds only down to a minimum of 0.9811, so a 0.1 step predicts a $9.48 saving where
        /// re-solving gives $5.03. 1% of the bound keeps the step inside that range.</para>
        /// </summary>
        internal static double ChooseDelta(bool isPercentage, double boundValue)
        {
            double magnitude = Math.Abs(boundValue);

            // A bound of zero gives no scale to work from (the demo data's ash minimum is 0),
            // so fall back to the smallest step that still reads naturally for the unit.
            if (magnitude <= 0.0)
            {
                return isPercentage ? 0.01 : 1.0;
            }

            double raw = magnitude * 0.01;

            // One significant figure, so a sentence says "by 30 kcal/kg" and not "by 29.5 kcal/kg".
            double scale = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            return Math.Round(raw / scale) * scale;
        }
    }
}
