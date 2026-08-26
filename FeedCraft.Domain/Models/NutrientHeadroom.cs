using System;
using System.Globalization;

namespace FeedCraft.Domain.Models
{
    /// <summary>How much room a nutrient has left against its target.</summary>
    public enum HeadroomStatus
    {
        /// <summary>No minimum and no maximum were set, so there is nothing to be close to.</summary>
        Unconstrained,

        /// <summary>Sitting on the minimum — the mix cannot give up any more of this.</summary>
        AtMinimum,

        /// <summary>Sitting on the maximum — the mix cannot take any more of this.</summary>
        AtMaximum,

        /// <summary>Minimum and maximum are the same value, so the level is pinned from both sides.</summary>
        Fixed,

        /// <summary>Strictly inside the target, with room to move in at least one direction.</summary>
        HasRoom
    }

    /// <summary>
    /// The distance between a solved nutrient level and its target, which is what makes the
    /// sensitivity table's <i>absences</i> readable: a nutrient is missing from it because it has
    /// slack, and this says how much.
    ///
    /// <para>Pure arithmetic on numbers the solve already produced — the achieved level and the
    /// user's own min/max. It reads no solver state, so it is as valid for a snapshot loaded from
    /// the database as for a fresh solve.</para>
    ///
    /// <para><see cref="Tolerance"/> is deliberately the same test
    /// <c>FeedOptimizationService</c> uses to decide which side of a target is binding. If the two
    /// disagreed, this column could report "0.00 below max" for a nutrient the sensitivity table
    /// is simultaneously calling "Maximum".</para>
    /// </summary>
    public class NutrientHeadroom
    {
        public int NutrientDefinitionId { get; set; }

        public HeadroomStatus Status { get; set; }

        /// <summary>The level the mix actually achieved, in the nutrient's own unit.</summary>
        public double Achieved { get; set; }

        /// <summary>How far above the minimum the level sits, or null when no minimum was set.</summary>
        public double? AboveMinimum { get; set; }

        /// <summary>How far below the maximum the level sits, or null when no maximum was set.</summary>
        public double? BelowMaximum { get; set; }

        /// <summary>
        /// The smaller of the two margins — the bound this nutrient would run into first. Zero
        /// when the level is already on a bound or nothing constrains it.
        /// </summary>
        public double TightestMargin
        {
            get
            {
                if (AboveMinimum.HasValue && BelowMaximum.HasValue)
                {
                    return Math.Min(AboveMinimum.Value, BelowMaximum.Value);
                }

                return AboveMinimum ?? BelowMaximum ?? 0.0;
            }
        }

        /// <summary>True when <see cref="TightestMargin"/> is measured down to the minimum.</summary>
        public bool TightestIsMinimum
        {
            get
            {
                if (!AboveMinimum.HasValue) return false;
                if (!BelowMaximum.HasValue) return true;
                return AboveMinimum.Value <= BelowMaximum.Value;
            }
        }

        public static NutrientHeadroom From(NutrientDefinition nutrient, NutrientConstraint? bounds,
            double achieved)
        {
            var headroom = new NutrientHeadroom
            {
                NutrientDefinitionId = nutrient.Id,
                Achieved = achieved,
                Status = HeadroomStatus.Unconstrained
            };

            double? min = bounds?.MinValue;
            double? max = bounds?.MaxValue;

            if (!min.HasValue && !max.HasValue)
            {
                return headroom;
            }

            // Clamped at zero: a level fractionally outside its bound is simplex dust, and a
            // margin of "-0.0000001 below max" would be read as an infeasible result.
            if (min.HasValue) headroom.AboveMinimum = Math.Max(0.0, achieved - min.Value);
            if (max.HasValue) headroom.BelowMaximum = Math.Max(0.0, max.Value - achieved);

            bool onMin = min.HasValue && achieved <= min.Value + Tolerance(min.Value);
            bool onMax = max.HasValue && achieved >= max.Value - Tolerance(max.Value);

            headroom.Status = onMin && onMax ? HeadroomStatus.Fixed
                            : onMin ? HeadroomStatus.AtMinimum
                            : onMax ? HeadroomStatus.AtMaximum
                            : HeadroomStatus.HasRoom;

            return headroom;
        }

        /// <summary>
        /// The short label for this headroom — "At maximum", or "2.96% above min".
        ///
        /// <para>Lives here rather than in the view because the CSV export prints the same words:
        /// two copies of this could describe the same solved mix differently. Numbers are formatted
        /// invariantly for that second consumer, so a decimal comma can never land inside a
        /// comma-delimited field.</para>
        /// </summary>
        public string Describe(string unit)
        {
            // "5.96%" but "2998.20 kcal/kg" — the space belongs to the word, not the sign.
            string label = unit == "%" ? "%" : " " + unit;

            switch (Status)
            {
                case HeadroomStatus.Unconstrained:
                    return "No target";

                case HeadroomStatus.Fixed:
                    return "Fixed target";

                case HeadroomStatus.AtMinimum:
                    return "At minimum";

                case HeadroomStatus.AtMaximum:
                    return "At maximum";

                default:
                    return TightestMargin.ToString("F2", CultureInfo.InvariantCulture) + label +
                           (TightestIsMinimum ? " above min" : " below max");
            }
        }

        /// <summary>
        /// Relative, so "on the bound" means the same thing for a 1.5% lysine target as for a
        /// 2950 kcal/kg energy target.
        /// </summary>
        public static double Tolerance(double bound) => 1e-6 * Math.Max(1.0, Math.Abs(bound));
    }
}
