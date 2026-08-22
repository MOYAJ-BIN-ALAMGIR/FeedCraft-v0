using Microsoft.AspNetCore.Mvc;
using FeedCraft_v0.Models;
using FeedCraft_v0.Services;
using System.Collections.Generic;
using System.Text.Json;

namespace FeedCraft_v0.Controllers
{
    public class FeedController : Controller
    {
        private readonly FeedOptimizationService _optimizer;

        public FeedController()
        {
            _optimizer = new FeedOptimizationService();
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new FeedFormulationViewModel
            {
                Ingredients = new List<Ingredient>
                {
                    new Ingredient { Name = "Maize", CostPerUnit = 0.20m, CrudeProteinPct = 8.5, FatPct = 3.5, LysinePct = 0.25, AshPct = 1.5, ME = 3300 },
                    new Ingredient { Name = "Soybean Meal", CostPerUnit = 0.35m, CrudeProteinPct = 44.0, FatPct = 1.5, LysinePct = 2.8, AshPct = 6.0, ME = 2200 },
                    new Ingredient { Name = "Mustard Meal", CostPerUnit = 0.25m, CrudeProteinPct = 35.0, FatPct = 8.0, LysinePct = 1.5, AshPct = 7.0, ME = 2800 },
                    // Adding Oil to satisfy high Energy requirements while allowing other ingredients to meet Protein
                    new Ingredient { Name = "Vegetable Oil", CostPerUnit = 0.90m, CrudeProteinPct = 0.0, FatPct = 100.0, LysinePct = 0.0, AshPct = 0.0, ME = 8800 }
                },
                Constraints = new List<NutrientConstraint>
                {
                    // Relaxing constraints slightly to ensure feasibility
                    new NutrientConstraint { NutrientName = "Crude Protein", MinValue = 20.0, MaxValue = 24.0 }, 
                    new NutrientConstraint { NutrientName = "Fat", MinValue = 3.0, MaxValue = 10.0 },
                    new NutrientConstraint { NutrientName = "Lysine", MinValue = 1.0, MaxValue = 1.5 },
                    new NutrientConstraint { NutrientName = "Ash", MinValue = 0.0, MaxValue = 8.0 },
                    new NutrientConstraint { NutrientName = "ME", MinValue = 2800, MaxValue = 3200 }
                },
                BatchSize = 1000
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Calculate(FeedFormulationViewModel model)
        {
            if (model.Ingredients == null || model.Ingredients.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Please add at least one ingredient.");
            }

            // Re-render the form (with validation messages) instead of running the
            // solver on invalid input — this is what prevents the zero-batch-size NaN.
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            var result = _optimizer.OptimizeFeed(model);

            // Post-Redirect-Get: stash the result and redirect so a refresh (F5)
            // re-issues a GET instead of re-submitting the form.
            TempData["FormulationResult"] = JsonSerializer.Serialize(result);
            return RedirectToAction(nameof(Result));
        }

        [HttpGet]
        public IActionResult Result()
        {
            // Peek (not read-once) so refreshing the results page keeps showing them.
            if (TempData.Peek("FormulationResult") is string json)
            {
                var model = JsonSerializer.Deserialize<FeedFormulationViewModel>(json);
                if (model != null)
                {
                    return View("Index", model);
                }
            }

            // Nothing to show (e.g. direct navigation) — start a fresh form.
            return RedirectToAction(nameof(Index));
        }
    }
}
