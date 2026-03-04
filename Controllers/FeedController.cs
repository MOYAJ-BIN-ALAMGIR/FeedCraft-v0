using Microsoft.AspNetCore.Mvc;
using FeedCraft_v0.Models;
using FeedCraft_v0.Services;
using System.Collections.Generic;

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
                    new Ingredient { Name = "Mustard Meal", CostPerUnit = 0.25m, CrudeProteinPct = 35.0, FatPct = 8.0, LysinePct = 1.5, AshPct = 7.0, ME = 2800 }
                },
                Constraints = new List<NutrientConstraint>
                {
                    new NutrientConstraint { NutrientName = "Crude Protein", MinValue = 22.0, MaxValue = 23.0 },
                    new NutrientConstraint { NutrientName = "Fat", MinValue = 5.0, MaxValue = 6.0 },
                    new NutrientConstraint { NutrientName = "Lysine", MinValue = 1.20, MaxValue = 1.35 },
                    new NutrientConstraint { NutrientName = "Ash", MinValue = 5.0, MaxValue = 8.0 },
                    new NutrientConstraint { NutrientName = "ME", MinValue = 3000, MaxValue = 3100 }
                },
                BatchSize = 1000
            };
            return View(model);
        }

        [HttpPost]
        public IActionResult Calculate(FeedFormulationViewModel model)
        {
            if (model.Ingredients == null || model.Ingredients.Count == 0)
            {
                ModelState.AddModelError("", "Please add at least one ingredient.");
                return View("Index", model);
            }

            var result = _optimizer.OptimizeFeed(model);
            return View("Index", result);
        }
    }
}
