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
            foreach (var constraint in model.Constraints)
            {
                var nutrient = model.FindNutrient(constraint.NutrientDefinitionId);
                if (nutrient == null) continue; // orphaned constraint; Normalize() drops these

                AddNutrientConstraint(solver, x, model.Ingredients, nutrient,
                                      constraint.MinValue, constraint.MaxValue, model.BatchSize);
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

                model.ErrorMessage = string.Empty;
            }
            else
            {
                model.IsSolved = false;
                model.ErrorMessage = "No optimal solution found. The problem might be infeasible (constraints cannot be met).";
            }

            return model;
        }

        private void AddNutrientConstraint(Solver solver, Variable[] x, List<Ingredient> ingredients,
            NutrientDefinition nutrient, double? minPerUnit, double? maxPerUnit, double batchSize)
        {
            if (!minPerUnit.HasValue && !maxPerUnit.HasValue) return;

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
        }
    }
}
