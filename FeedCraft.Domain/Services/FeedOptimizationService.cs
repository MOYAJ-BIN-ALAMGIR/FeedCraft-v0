using Google.OrTools.LinearSolver;
using FeedCraft.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FeedCraft.Domain.Services
{
    public class FeedOptimizationService : IFeedOptimizationService
    {
        public FeedFormulationViewModel OptimizeFeed(FeedFormulationViewModel model)
        {
            // Make sure Ids and nutrient-value slots are consistent before we build the LP.
            model.Normalize();

            // 0. Validate per-ingredient inclusion limits before touching the solver.
            //    (Null Min => 0%, null Max => 100%.) A Max below Min is unsolvable, so
            //    report it as a model error rather than handing bad bounds to OR-Tools.
            foreach (var ingredient in model.Ingredients)
            {
                double minPct = ingredient.MinInclusionPct ?? 0.0;
                double maxPct = ingredient.MaxInclusionPct ?? 100.0;
                if (maxPct < minPct)
                {
                    model.IsSolved = false;
                    model.ErrorMessage =
                        $"Ingredient \"{ingredient.Name}\": maximum inclusion ({maxPct:0.##}%) " +
                        $"cannot be less than minimum inclusion ({minPct:0.##}%).";
                    return model;
                }
            }

            // 0b. Percentage nutrients must stay within 0–100: no single component can make up
            //     more than all of an ingredient. This is where IsPercentage carries real weight —
            //     it excludes absolute units, where ME = 3300 kcal/kg is perfectly normal.
            //
            //     Note we deliberately check each value individually and do NOT require the
            //     percentages to sum to <= 100 within an ingredient: nutrients nest (Lysine is
            //     part of Crude Protein), so a sum check would reject valid feed data.
            foreach (var nutrient in model.NutrientDefinitions.Where(n => n.IsPercentage))
            {
                foreach (var ingredient in model.Ingredients)
                {
                    double value = ingredient.GetNutrientValue(nutrient.Id);
                    if (value < 0.0 || value > 100.0)
                    {
                        model.IsSolved = false;
                        model.ErrorMessage =
                            $"\"{ingredient.Name}\" has {nutrient.Name} = {value:0.##}{nutrient.Unit}. " +
                            $"{nutrient.Name} is marked as a percentage, so it must be between 0 and 100. " +
                            $"Untick \"Is %\" if it is an absolute unit.";
                        return model;
                    }
                }

                var bounds = model.FindConstraint(nutrient.Id);
                double?[] limits = { bounds?.MinValue, bounds?.MaxValue };
                foreach (var limit in limits)
                {
                    if (limit.HasValue && (limit.Value < 0.0 || limit.Value > 100.0))
                    {
                        model.IsSolved = false;
                        model.ErrorMessage =
                            $"{nutrient.Name} target of {limit.Value:0.##} is out of range. " +
                            $"{nutrient.Name} is marked as a percentage, so its Min and Max must be between 0 and 100.";
                        return model;
                    }
                }
            }

            // 1. Create the solver
            Solver solver = Solver.CreateSolver("GLOP");
            if (solver == null)
            {
                model.ErrorMessage = "Could not create solver (GLOP). Ensure Google.OrTools is installed.";
                model.IsSolved = false;
                return model;
            }

            // 2. Define Variables
            // x[i] represents the quantity of ingredient i, bounded by its inclusion limits:
            //   lower = (MinInclusionPct/100) * BatchSize,  upper = (MaxInclusionPct/100) * BatchSize
            Variable[] x = new Variable[model.Ingredients.Count];
            for (int i = 0; i < model.Ingredients.Count; i++)
            {
                double minPct = model.Ingredients[i].MinInclusionPct ?? 0.0;
                double maxPct = model.Ingredients[i].MaxInclusionPct ?? 100.0;
                double lowerBound = (minPct / 100.0) * model.BatchSize;
                double upperBound = (maxPct / 100.0) * model.BatchSize;
                x[i] = solver.MakeNumVar(lowerBound, upperBound, model.Ingredients[i].Name);
            }

            // 3. Define Constraints

            // 3a. Total Batch Size Constraint: Sum(x[i]) = BatchSize
            Constraint totalWeight = solver.MakeConstraint(model.BatchSize, model.BatchSize);
            for (int i = 0; i < model.Ingredients.Count; i++)
            {
                totalWeight.SetCoefficient(x[i], 1);
            }

            // 3b. Nutrient Constraints — driven entirely by the nutrient definitions,
            //     so a new nutrient participates in the LP with no code changes.
            //
            //     The Constraint objects are kept, keyed by nutrient, because step 7 below reads
            //     their dual values. GLOP is a linear solver, so those duals are meaningful; if
            //     CreateSolver is ever pointed at a MIP backend (CBC, SCIP) the duals become
            //     meaningless and the sensitivity output must be suppressed, not reinterpreted.
            var nutrientConstraints = new Dictionary<int, Constraint>();
            foreach (var constraint in model.Constraints)
            {
                var nutrient = model.FindNutrient(constraint.NutrientDefinitionId);
                if (nutrient == null) continue; // orphaned constraint; Normalize() drops these

                Constraint? lpConstraint = AddNutrientConstraint(solver, x, model.Ingredients, nutrient,
                                                                constraint.MinValue, constraint.MaxValue, model.BatchSize);

                if (lpConstraint != null)
                {
                    nutrientConstraints[nutrient.Id] = lpConstraint;
                }
            }

            // 4. Define Objective Function: Minimize Cost
            Objective objective = solver.Objective();
            for (int i = 0; i < model.Ingredients.Count; i++)
            {
                objective.SetCoefficient(x[i], (double)model.Ingredients[i].CostPerUnit);
            }
            objective.SetMinimization();

            // 5. Solve
            Solver.ResultStatus resultStatus = solver.Solve();

            // 6. Process Results
            if (resultStatus == Solver.ResultStatus.OPTIMAL || resultStatus == Solver.ResultStatus.FEASIBLE)
            {
                model.IsSolved = true;
                model.TotalCost = (decimal)objective.Value();
                model.OptimizedQuantities = new Dictionary<int, double>();

                for (int i = 0; i < model.Ingredients.Count; i++)
                {
                    model.OptimizedQuantities[model.Ingredients[i].Id] = x[i].SolutionValue();
                }

                // Resulting nutrient levels, for every defined nutrient — including
                // ones with no min/max, so they can still be inspected.
                model.CalculatedNutrients = new Dictionary<int, double>();
                foreach (var nutrient in model.NutrientDefinitions)
                {
                    double totalNutrientAmount = 0;
                    for (int i = 0; i < model.Ingredients.Count; i++)
                    {
                        double quantity = x[i].SolutionValue();
                        totalNutrientAmount += quantity * model.Ingredients[i].GetNutrientValue(nutrient.Id);
                    }
                    // Weighted average per unit of batch (e.g. % or kcal/kg).
                    model.CalculatedNutrients[nutrient.Id] = totalNutrientAmount / model.BatchSize;
                }

                // 7. Sensitivity — the marginal cost of each binding nutrient target. Must come
                //    after CalculatedNutrients, which is what identifies the binding side.
                model.ShadowPrices = BuildShadowPrices(model, nutrientConstraints);
                model.SensitivityComputed = true;

                // 7b. The other half of the same question: not what the targets cost, but which
                //     ingredients are mispriced for this mix. Reads the variables' reduced costs,
                //     which like the duals are already sitting in the solved LP.
                model.ReducedCosts = BuildReducedCosts(model, x);

                model.ErrorMessage = string.Empty;
            }
            else
            {
                model.IsSolved = false;
                model.ErrorMessage = "No optimal solution found. The problem might be infeasible (constraints cannot be met).";
            }

            return model;
        }

        private Constraint? AddNutrientConstraint(Solver solver, Variable[] x, List<Ingredient> ingredients,
            NutrientDefinition nutrient, double? minPerUnit, double? maxPerUnit, double batchSize)
        {
            if (!minPerUnit.HasValue && !maxPerUnit.HasValue) return null;

            // The constraint itself is unit-agnostic: min/max are expressed in the same unit as the
            // ingredient values, so this works for "%" and "kcal/kg" alike without conversion.
            //
            //   Sum( value_i * x_i )  in  [ min * batchSize , max * batchSize ]
            //
            // Dividing through by batchSize shows what this means: the batch's weighted-average
            // nutrient level must sit between min and max. IsPercentage deliberately does not
            // appear here — scaling both the coefficients and the bounds by 100 would cancel out.
            // It is enforced as a range check before the solve instead (see step 0b).
            double lowerBound = minPerUnit.HasValue
                ? minPerUnit.Value * batchSize
                : double.NegativeInfinity;

            double upperBound = maxPerUnit.HasValue
                ? maxPerUnit.Value * batchSize
                : double.PositiveInfinity;

            Constraint constraint = solver.MakeConstraint(lowerBound, upperBound);

            for (int i = 0; i < ingredients.Count; i++)
            {
                constraint.SetCoefficient(x[i], ingredients[i].GetNutrientValue(nutrient.Id));
            }

            return constraint;
        }

        /// <summary>
        /// Anything smaller than this is floating-point dust from the simplex, not a real
        /// marginal cost. The threshold exists to reject noise, not to make a judgement about
        /// which constraints are worth reporting.
        /// </summary>
        private const double DualTolerance = 1e-9;

        /// <summary>
        /// Reads the dual value of every binding nutrient constraint and turns it into money.
        /// Only meaningful after a successful solve, and only because the solver is an LP —
        /// see the note at step 3b.
        ///
        /// A dual answers "if this requirement moved by one unit, what would that do to the
        /// batch cost?". Zero means the mix is not pressed against the target at all, so it is
        /// skipped: what comes back is a list of what is actually driving the price, not a row
        /// per nutrient.
        /// </summary>
        private Dictionary<int, NutrientShadowPrice> BuildShadowPrices(
            FeedFormulationViewModel model, Dictionary<int, Constraint> nutrientConstraints)
        {
            var shadowPrices = new Dictionary<int, NutrientShadowPrice>();

            foreach (var pair in nutrientConstraints)
            {
                var nutrient = model.FindNutrient(pair.Key);
                var bounds = model.FindConstraint(pair.Key);
                if (nutrient == null || bounds == null) continue;

                double dual = pair.Value.DualValue();
                if (Math.Abs(dual) <= DualTolerance) continue;

                if (TryFindBindingSide(model.CalculatedNutrients, pair.Key, bounds,
                                       out BindingSide side, out double boundValue))
                {
                    shadowPrices[pair.Key] = NutrientShadowPrice.From(nutrient, side, boundValue, dual);
                }
            }

            return shadowPrices;
        }

        /// <summary>
        /// Reads the reduced cost of every ingredient variable and turns it into a verdict on that
        /// ingredient's price. Same preconditions as <see cref="BuildShadowPrices"/>: after a
        /// successful solve, and only valid because the solver is an LP.
        ///
        /// A reduced cost is zero for any ingredient the optimizer chose freely — it is worth
        /// exactly what it costs here — so those are skipped and what comes back is a list of the
        /// ingredients whose price is the reason they are stuck on an inclusion limit.
        /// </summary>
        private Dictionary<int, IngredientReducedCost> BuildReducedCosts(
            FeedFormulationViewModel model, Variable[] x)
        {
            var reducedCosts = new Dictionary<int, IngredientReducedCost>();

            for (int i = 0; i < model.Ingredients.Count; i++)
            {
                double reduced = x[i].ReducedCost();
                if (Math.Abs(reduced) <= DualTolerance) continue;

                var ingredient = model.Ingredients[i];

                // The same bounds step 2 gave the variable, recomputed rather than stored: they
                // are a pure function of the inclusion percentages and the batch size.
                double lower = (ingredient.MinInclusionPct ?? 0.0) / 100.0 * model.BatchSize;
                double upper = (ingredient.MaxInclusionPct ?? 100.0) / 100.0 * model.BatchSize;
                double quantity = x[i].SolutionValue();

                InclusionVerdict verdict;

                if (quantity <= lower + NutrientHeadroom.Tolerance(lower))
                {
                    // Resting on its floor. A floor of zero means the optimizer simply refused to
                    // buy it; a floor above zero means the user's own minimum is forcing it in.
                    verdict = lower > 0.0
                        ? InclusionVerdict.HeldInByMinimum
                        : InclusionVerdict.PricedOut;
                }
                else if (quantity >= upper - NutrientHeadroom.Tolerance(upper))
                {
                    verdict = InclusionVerdict.CappedByMaximum;
                }
                else
                {
                    // Strictly inside its limits, so the reduced cost should have been zero and
                    // this is numerical noise above the tolerance. Reporting nothing beats
                    // inventing a verdict about a variable the optimizer chose freely.
                    continue;
                }

                reducedCosts[ingredient.Id] =
                    IngredientReducedCost.From(ingredient, verdict, reduced, quantity);
            }

            return reducedCosts;
        }

        /// <summary>
        /// Works out which end of a min/max range the achieved level is sitting on, by comparing
        /// it against the bounds rather than by reading the dual's sign.
        ///
        /// One OR-Tools Constraint carries both bounds at once, so the side has to be inferred
        /// from somewhere. Splitting each range into two single-sided constraints would answer it
        /// directly, but that changes the shape of the LP, and on a degenerate vertex a different
        /// row count can resolve ties differently and quietly move a known-good result. The
        /// achieved level is already computed above, so this costs nothing and leaves the LP alone.
        ///
        /// Returns false only if the level is on neither bound, which a non-zero dual makes
        /// impossible — an active constraint sits on one of its bounds by definition. Reporting
        /// nothing beats reporting a side that might be the wrong one.
        /// </summary>
        private static bool TryFindBindingSide(Dictionary<int, double> calculatedNutrients,
            int nutrientId, NutrientConstraint bounds, out BindingSide side, out double boundValue)
        {
            side = BindingSide.Minimum;
            boundValue = 0.0;

            if (!bounds.MaxValue.HasValue)
            {
                if (!bounds.MinValue.HasValue) return false;
                side = BindingSide.Minimum;
                boundValue = bounds.MinValue.Value;
                return true;
            }

            if (!bounds.MinValue.HasValue)
            {
                side = BindingSide.Maximum;
                boundValue = bounds.MaxValue.Value;
                return true;
            }

            double min = bounds.MinValue.Value;
            double max = bounds.MaxValue.Value;
            double achieved = calculatedNutrients.TryGetValue(nutrientId, out var level) ? level : 0.0;

            bool onMin = achieved <= min + Tolerance(min);
            bool onMax = achieved >= max - Tolerance(max);

            if (onMin && onMax)
            {
                // min == max, so the level is pinned from both directions at once.
                side = BindingSide.Fixed;
                boundValue = min;
                return true;
            }

            if (onMin)
            {
                side = BindingSide.Minimum;
                boundValue = min;
                return true;
            }

            if (onMax)
            {
                side = BindingSide.Maximum;
                boundValue = max;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Relative, so "on the bound" means the same thing for a 1.5% lysine target as for a
        /// 2950 kcal/kg energy target.
        ///
        /// Delegates rather than repeating the formula: the nutrient-analysis table decides whether
        /// to print "At maximum" or "1.96% below max" using the same test, and the two disagreeing
        /// would let one table contradict the other by a rounding error.
        /// </summary>
        private static double Tolerance(double bound) => NutrientHeadroom.Tolerance(bound);
    }
}
