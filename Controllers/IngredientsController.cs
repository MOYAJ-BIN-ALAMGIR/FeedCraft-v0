using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FeedCraft.Domain.Models;
using FeedCraft.Infrastructure.Data;
using FeedCraft.Web.Models;
using System.Collections.Generic;
using System.Linq;

namespace FeedCraft.Web.Controllers
{
    /// <summary>
    /// CRUD over the persisted ingredient library — the master list the formulation page
    /// starts from. Nutrient *definitions* are not edited here: they are seeded, and the
    /// formulation page's "Add Nutrient" button stays a transient, per-formulation thing.
    ///
    /// Unlike FeedController, this controller works with tracked entities and must never
    /// call FeedFormulationViewModel.Normalize() — that method reassigns Ids, which would
    /// corrupt library rows.
    /// </summary>
    public class IngredientsController : Controller
    {
        private readonly FeedCraftDbContext _db;

        public IngredientsController(FeedCraftDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new IngredientLibraryViewModel
            {
                Nutrients = LoadNutrients(),
                Ingredients = _db.Ingredients
                    .AsNoTracking()
                    .Include(i => i.NutrientValues)
                    .OrderBy(i => i.Id)
                    .ToList()
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var nutrients = LoadNutrients();

            var ingredient = new Ingredient
            {
                // One empty reading per nutrient, so the form renders a full row of inputs.
                NutrientValues = nutrients
                    .Select(n => new IngredientNutrientValue { NutrientDefinitionId = n.Id })
                    .ToList()
            };

            return View(new IngredientEditViewModel { Ingredient = ingredient, Nutrients = nutrients });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(IngredientEditViewModel form)
        {
            var nutrients = LoadNutrients();
            ValidateInclusionRange(form.Ingredient);

            if (!ModelState.IsValid)
            {
                form.Nutrients = nutrients;
                return View(form);
            }

            var ingredient = new Ingredient
            {
                Name = form.Ingredient.Name.Trim(),
                CostPerUnit = form.Ingredient.CostPerUnit,
                MinInclusionPct = form.Ingredient.MinInclusionPct,
                MaxInclusionPct = form.Ingredient.MaxInclusionPct
            };

            // Build readings from the nutrient list rather than from the post, so the row is
            // complete even if the form omitted a nutrient. IngredientId is left at 0 — EF
            // fills it in during SaveChanges once the new ingredient has an identity.
            foreach (var nutrient in nutrients)
            {
                ingredient.NutrientValues.Add(new IngredientNutrientValue
                {
                    NutrientDefinitionId = nutrient.Id,
                    Value = PostedValue(form, nutrient.Id)
                });
            }

            _db.Ingredients.Add(ingredient);
            _db.SaveChanges();

            TempData["LibraryMessage"] = $"Added \"{ingredient.Name}\" to the library.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var nutrients = LoadNutrients();

            var ingredient = _db.Ingredients
                .AsNoTracking()
                .Include(i => i.NutrientValues)
                .FirstOrDefault(i => i.Id == id);

            if (ingredient == null)
            {
                return NotFound();
            }

            // Gap-fill so a nutrient added after this ingredient was created still renders.
            foreach (var nutrient in nutrients)
            {
                if (ingredient.NutrientValues.All(v => v.NutrientDefinitionId != nutrient.Id))
                {
                    ingredient.NutrientValues.Add(new IngredientNutrientValue
                    {
                        IngredientId = ingredient.Id,
                        NutrientDefinitionId = nutrient.Id
                    });
                }
            }

            ingredient.NutrientValues = SortByNutrientOrder(ingredient.NutrientValues, nutrients);

            return View(new IngredientEditViewModel
            {
                Ingredient = ingredient,
                Nutrients = nutrients,
                IsEdit = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, IngredientEditViewModel form)
        {
            var nutrients = LoadNutrients();
            ValidateInclusionRange(form.Ingredient);

            if (!ModelState.IsValid)
            {
                form.Ingredient.Id = id;
                form.Nutrients = nutrients;
                form.IsEdit = true;
                return View(form);
            }

            var ingredient = _db.Ingredients
                .Include(i => i.NutrientValues)
                .FirstOrDefault(i => i.Id == id);

            if (ingredient == null)
            {
                return NotFound();
            }

            ingredient.Name = form.Ingredient.Name.Trim();
            ingredient.CostPerUnit = form.Ingredient.CostPerUnit;
            ingredient.MinInclusionPct = form.Ingredient.MinInclusionPct;
            ingredient.MaxInclusionPct = form.Ingredient.MaxInclusionPct;

            // Upsert each reading in place. Replacing the collection wholesale would make EF
            // delete and re-insert rows whose primary key is (IngredientId, NutrientDefinitionId).
            foreach (var nutrient in nutrients)
            {
                var existing = ingredient.NutrientValues
                    .FirstOrDefault(v => v.NutrientDefinitionId == nutrient.Id);

                if (existing == null)
                {
                    ingredient.NutrientValues.Add(new IngredientNutrientValue
                    {
                        IngredientId = ingredient.Id,
                        NutrientDefinitionId = nutrient.Id,
                        Value = PostedValue(form, nutrient.Id)
                    });
                }
                else
                {
                    existing.Value = PostedValue(form, nutrient.Id);
                }
            }

            _db.SaveChanges();

            TempData["LibraryMessage"] = $"Updated \"{ingredient.Name}\".";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            // Include the readings so EF deletes them explicitly rather than relying on the
            // database's ON DELETE CASCADE (which needs foreign keys enabled on the connection).
            var ingredient = _db.Ingredients
                .Include(i => i.NutrientValues)
                .FirstOrDefault(i => i.Id == id);

            if (ingredient == null)
            {
                return NotFound();
            }

            var name = ingredient.Name;
            _db.Ingredients.Remove(ingredient);
            _db.SaveChanges();

            TempData["LibraryMessage"] = $"Deleted \"{name}\". Saved formulations are unaffected.";
            return RedirectToAction(nameof(Index));
        }

        private List<NutrientDefinition> LoadNutrients() =>
            _db.NutrientDefinitions
                .AsNoTracking()
                .OrderBy(n => n.Id)
                .ToList();

        /// <summary>The posted reading for one nutrient, or 0 when the form carried none.</summary>
        private static double PostedValue(IngredientEditViewModel form, int nutrientDefinitionId) =>
            form.Ingredient.NutrientValues
                .FirstOrDefault(v => v.NutrientDefinitionId == nutrientDefinitionId)?.Value ?? 0.0;

        /// <summary>
        /// Caught here as well as in the solver: an ingredient stored with Max below Min
        /// would fail every future solve until someone found and fixed the row.
        /// </summary>
        private void ValidateInclusionRange(Ingredient ingredient)
        {
            var min = ingredient.MinInclusionPct;
            var max = ingredient.MaxInclusionPct;

            if (min.HasValue && max.HasValue && max.Value < min.Value)
            {
                ModelState.AddModelError("Ingredient.MaxInclusionPct",
                    "Max inclusion % cannot be less than min inclusion %.");
            }
        }

        private static List<IngredientNutrientValue> SortByNutrientOrder(
            List<IngredientNutrientValue> values, List<NutrientDefinition> nutrients)
        {
            var order = nutrients
                .Select((n, index) => new { n.Id, index })
                .ToDictionary(x => x.Id, x => x.index);

            return values
                .OrderBy(v => order.TryGetValue(v.NutrientDefinitionId, out var i) ? i : int.MaxValue)
                .ToList();
        }
    }
}
