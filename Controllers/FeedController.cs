using Microsoft.AspNetCore.Mvc;
using FeedCraft.Domain.Models;
using FeedCraft.Domain.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FeedCraft.Web.Controllers
{
    public class FeedController : Controller
    {
        private readonly IFeedOptimizationService _optimizer;

        public FeedController(IFeedOptimizationService optimizer)
        {
            _optimizer = optimizer;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(BuildDefaultModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Calculate(FeedFormulationViewModel model)
        {
            // Repair Ids / nutrient slots for anything the client added dynamically
            // before we validate or render.
            model.Normalize();

            if (model.Ingredients == null || model.Ingredients.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Please add at least one ingredient.");
            }

            if (model.NutrientDefinitions == null || model.NutrientDefinitions.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Please define at least one nutrient.");
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

        /// <summary>
        /// Seed data. Nutrients are now rows rather than properties, so this list
        /// is the only place the default five are named.
        /// </summary>
        private static FeedFormulationViewModel BuildDefaultModel()
        {
            var nutrients = new List<NutrientDefinition>
            {
                new NutrientDefinition { Id = 1, Name = "Crude Protein", Unit = "%",       IsPercentage = true  },
                new NutrientDefinition { Id = 2, Name = "Fat",           Unit = "%",       IsPercentage = true  },
                new NutrientDefinition { Id = 3, Name = "Lysine",        Unit = "%",       IsPercentage = true  },
                new NutrientDefinition { Id = 4, Name = "Ash",           Unit = "%",       IsPercentage = true  },
                new NutrientDefinition { Id = 5, Name = "ME",            Unit = "kcal/kg", IsPercentage = false }
            };

            // Values are in nutrient-definition order: CP, Fat, Lysine, Ash, ME.
            var model = new FeedFormulationViewModel
            {
                NutrientDefinitions = nutrients,
                Ingredients = new List<Ingredient>
                {
                    MakeIngredient(1, "Maize",         0.20m, nutrients, 8.5,  3.5,   0.25, 1.5, 3300),
                    MakeIngredient(2, "Soybean Meal",  0.35m, nutrients, 44.0, 1.5,   2.8,  6.0, 2200),
                    MakeIngredient(3, "Mustard Meal",  0.25m, nutrients, 35.0, 8.0,   1.5,  7.0, 2800),
                    // Oil satisfies high energy requirements while letting other ingredients meet protein
                    MakeIngredient(4, "Vegetable Oil", 0.90m, nutrients, 0.0,  100.0, 0.0,  0.0, 8800)
                },
                Constraints = new List<NutrientConstraint>
                {
                    new NutrientConstraint { NutrientDefinitionId = 1, MinValue = 20.0,   MaxValue = 24.0   },
                    new NutrientConstraint { NutrientDefinitionId = 2, MinValue = 3.0,    MaxValue = 10.0   },
                    new NutrientConstraint { NutrientDefinitionId = 3, MinValue = 1.0,    MaxValue = 1.5    },
                    new NutrientConstraint { NutrientDefinitionId = 4, MinValue = 0.0,    MaxValue = 8.0    },
                    new NutrientConstraint { NutrientDefinitionId = 5, MinValue = 2800.0, MaxValue = 3200.0 }
                },
                BatchSize = 1000
            };

            model.Normalize();
            return model;
        }

        private static Ingredient MakeIngredient(int id, string name, decimal cost,
            List<NutrientDefinition> nutrients, params double[] values)
        {
            var ingredient = new Ingredient { Id = id, Name = name, CostPerUnit = cost };

            ingredient.NutrientValues = nutrients
                .Select((nutrient, index) => new IngredientNutrientValue
                {
                    IngredientId = id,
                    NutrientDefinitionId = nutrient.Id,
                    Value = index < values.Length ? values[index] : 0.0
                })
                .ToList();

            return ingredient;
        }
    }
}
