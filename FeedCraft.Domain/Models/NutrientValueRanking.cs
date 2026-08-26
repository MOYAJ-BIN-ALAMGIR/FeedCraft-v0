using System;
using System.Collections.Generic;
using System.Linq;

namespace FeedCraft.Domain.Models
{
    /// <summary>
    /// What one ingredient charges for the nutrient itself, rather than for its own mass.
    /// </summary>
    public class NutrientSourceCost
    {
        public int IngredientId { get; set; }

        public string IngredientName { get; set; } = string.Empty;

        /// <summary>The ingredient's own price, $ per kg as entered.</summary>
        public decimal CostPerUnit { get; set; }

        /// <summary>The nutrient reading for this ingredient, in the nutrient's own unit.</summary>
        public double NutrientContent { get; set; }

        /// <summary>
        /// The price of the nutrient bought through this ingredient, per
        /// <see cref="NutrientValueRanking.Basis"/> — $ per kg for a percentage nutrient,
        /// $ per 1,000 units for an absolute one.
        /// </summary>
        public double CostPerNutrientUnit { get; set; }
    }

    /// <summary>
    /// Every ingredient ranked by what it charges for one nutrient, cheapest first — the
    /// "who is the cheapest protein?" question a nutritionist asks before the optimizer is
    /// involved at all.
    ///
    /// <para><b>The arithmetic.</b> A percentage nutrient at <c>p%</c> means one kg of the
    /// ingredient carries <c>p/100</c> kg of the nutrient, so the nutrient costs
    /// <c>CostPerUnit * 100 / p</c> per kg. An absolute nutrient (energy in kcal/kg, say) is
    /// quoted per 1,000 units instead — <c>CostPerUnit * 1000 / content</c> — because $ per
    /// single kcal rounds to nothing and the feed industry quotes energy per Mcal anyway.</para>
    ///
    /// <para>Pure arithmetic over the ingredient list: no solver, no constraints, no batch size.
    /// It is therefore valid before anything has been solved, and answers a different question
    /// from the sensitivity table — that one prices <i>this mix's</i> binding targets, this one
    /// prices the raw materials. A cheap source can still be absent from the optimum because
    /// some other nutrient rules it out.</para>
    /// </summary>
    public class NutrientValueRanking
    {
        /// <summary>Percent to fraction: a reading of 44% is 0.44 kg of nutrient per kg.</summary>
        public const double PercentToWhole = 100.0;

        /// <summary>Absolute nutrients are quoted per this many units, not per single unit.</summary>
        public const double AbsoluteBasis = 1000.0;

        public int NutrientDefinitionId { get; set; }

        public string NutrientName { get; set; } = string.Empty;

        /// <summary>The nutrient's display unit, e.g. "%" or "kcal/kg".</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// What <see cref="NutrientSourceCost.CostPerNutrientUnit"/> is priced per — "kg" for a
        /// percentage nutrient, "1,000 kcal" and friends for an absolute one.
        /// </summary>
        public string Basis { get; set; } = string.Empty;

        /// <summary>Ingredients that supply this nutrient, cheapest source first.</summary>
        public List<NutrientSourceCost> Sources { get; set; } = new();

        /// <summary>
        /// Names of ingredients whose reading is zero. Left out of the ranking rather than
        /// sorted last: they are not expensive sources, they are not sources at all, and
        /// dividing by zero would rank them as infinitely expensive.
        /// </summary>
        public List<string> NonSuppliers { get; set; } = new();

        /// <summary>The best buy, or null when nothing on the list supplies this nutrient.</summary>
        public NutrientSourceCost? Cheapest => Sources.Count > 0 ? Sources[0] : null;

        /// <summary>
        /// Ranks every ingredient against every nutrient. Nutrients are returned in the order
        /// given, so the table lines up with the rest of the page.
        /// </summary>
        public static List<NutrientValueRanking> Build(
            IList<NutrientDefinition> nutrients, IList<Ingredient> ingredients)
        {
            var rankings = new List<NutrientValueRanking>();
            if (nutrients == null || ingredients == null)
            {
                return rankings;
            }

            foreach (var nutrient in nutrients)
            {
                var ranking = new NutrientValueRanking
                {
                    NutrientDefinitionId = nutrient.Id,
                    NutrientName = nutrient.Name,
                    Unit = nutrient.Unit,
                    Basis = DescribeBasis(nutrient)
                };

                double scale = nutrient.IsPercentage ? PercentToWhole : AbsoluteBasis;
                var supplying = new List<NutrientSourceCost>();

                foreach (var ingredient in ingredients)
                {
                    double content = ingredient.GetNutrientValue(nutrient.Id);

                    // GetNutrientValue returns 0 both for "recorded as zero" and for "no reading
                    // at all"; either way this ingredient cannot buy you the nutrient.
                    if (content <= 0.0)
                    {
                        ranking.NonSuppliers.Add(ingredient.Name);
                        continue;
                    }

                    supplying.Add(new NutrientSourceCost
                    {
                        IngredientId = ingredient.Id,
                        IngredientName = ingredient.Name,
                        CostPerUnit = ingredient.CostPerUnit,
                        NutrientContent = content,
                        CostPerNutrientUnit = (double)ingredient.CostPerUnit * scale / content
                    });
                }

                // OrderBy is a stable sort, so equally-priced sources keep the order the
                // ingredient grid shows them in. List.Sort would not guarantee that.
                ranking.Sources = supplying.OrderBy(s => s.CostPerNutrientUnit).ToList();
                rankings.Add(ranking);
            }

            return rankings;
        }

        /// <summary>
        /// The label the cost is quoted per. A percentage nutrient is priced per kg of the
        /// nutrient itself; an absolute one is priced per 1,000 of whatever it is measured in,
        /// which is the numerator of its unit — "kcal/kg" gives "1,000 kcal", "IU/kg" gives
        /// "1,000 IU".
        /// </summary>
        internal static string DescribeBasis(NutrientDefinition nutrient)
        {
            if (nutrient.IsPercentage)
            {
                return "kg";
            }

            string unit = nutrient.Unit ?? string.Empty;
            int slash = unit.IndexOf('/');
            string measure = (slash >= 0 ? unit.Substring(0, slash) : unit).Trim();

            return string.IsNullOrEmpty(measure) ? "unit" : "1,000 " + measure;
        }
    }
}
