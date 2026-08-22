using Google.OrTools.LinearSolver;
using FeedCraft_v0.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FeedCraft_v0.Services
{
    public class FeedOptimizationService
    {
        public FeedFormulationViewModel OptimizeFeed(FeedFormulationViewModel model)
        {
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

            // 3b. Nutrient Constraints
            foreach (var constraint in model.Constraints)
            {
                AddConstraint(solver, x, model.Ingredients, constraint.NutrientName, constraint.MinValue, constraint.MaxValue, model.BatchSize);
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
                model.OptimizedQuantities = new Dictionary<string, double>();
                
                for (int i = 0; i < model.Ingredients.Count; i++)
                {
                    model.OptimizedQuantities[model.Ingredients[i].Name] = x[i].SolutionValue();
                }

                // Calculate resulting nutrient values
                model.CalculatedNutrients = new Dictionary<string, double>();
                foreach (var constraint in model.Constraints)
                {
                    double totalNutrientAmount = 0;
                    for (int i = 0; i < model.Ingredients.Count; i++)
                    {
                        double quantity = x[i].SolutionValue();
                        double nutrientValue = GetNutrientValue(model.Ingredients[i], constraint.NutrientName);
                        totalNutrientAmount += quantity * nutrientValue;
                    }
                    // The result is the weighted average per unit (e.g. % or kcal/kg)
                    model.CalculatedNutrients[constraint.NutrientName] = totalNutrientAmount / model.BatchSize;
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

        private void AddConstraint(Solver solver, Variable[] x, List<Ingredient> ingredients, string nutrientName, double? minPerUnit, double? maxPerUnit, double batchSize)
        {
            if (!minPerUnit.HasValue && !maxPerUnit.HasValue) return;

            // Determine if the nutrient is percentage-based or absolute per unit (like ME kcal/kg)
            bool isPercentage = !nutrientName.Equals("ME", StringComparison.OrdinalIgnoreCase) && !nutrientName.Equals("Metabolizable Energy", StringComparison.OrdinalIgnoreCase);

            double lowerBound = double.NegativeInfinity;
            double upperBound = double.PositiveInfinity;

            // If it's a percentage (e.g. 22%), the total amount required is (22/100) * BatchSize.
            // If it's absolute (e.g. 3000 kcal/kg), the total amount required is 3000 * BatchSize.
            
            if (minPerUnit.HasValue)
            {
                lowerBound = isPercentage 
                    ? (minPerUnit.Value / 100.0) * batchSize 
                    : minPerUnit.Value * batchSize;
            }

            if (maxPerUnit.HasValue)
            {
                upperBound = isPercentage 
                    ? (maxPerUnit.Value / 100.0) * batchSize 
                    : maxPerUnit.Value * batchSize;
            }

            Constraint constraint = solver.MakeConstraint(lowerBound, upperBound);
            
            for (int i = 0; i < ingredients.Count; i++)
            {
                double val = GetNutrientValue(ingredients[i], nutrientName);
                
                // If ingredient has 22% CP, the value is 22.
                // The constraint is Sum( (val/100) * x_i ) >= (Min/100)*BatchSize
                // Or simply Sum( val * x_i ) >= Min * BatchSize?
                // Let's stick to mass units. 
                // LHS: Sum of nutrient mass. 
                // If x_i is kg, and val is %, then nutrient mass = (val/100) * x_i.
                // RHS: Total nutrient mass required = (Min/100) * BatchSize.
                
                if (isPercentage)
                {
                    constraint.SetCoefficient(x[i], val / 100.0);
                }
                else
                {
                    // For ME (kcal/kg):
                    // LHS: Sum( ME_i * x_i ) = Total Kcal.
                    // RHS: Min_ME * BatchSize.
                    constraint.SetCoefficient(x[i], val);
                }
            }
        }

        private double GetNutrientValue(Ingredient ingredient, string nutrientName)
        {
            // Normalize name
            string name = nutrientName.ToLower().Replace(" ", "");
            
            if (name.Contains("protein") || name == "cp") return ingredient.CrudeProteinPct;
            if (name.Contains("fat")) return ingredient.FatPct;
            if (name.Contains("lysine")) return ingredient.LysinePct;
            if (name.Contains("ash")) return ingredient.AshPct;
            if (name == "me" || name.Contains("energy")) return ingredient.ME;
            
            return 0.0;
        }
    }
}
