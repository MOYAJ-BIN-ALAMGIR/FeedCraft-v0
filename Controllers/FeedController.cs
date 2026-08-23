using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FeedCraft.Domain.Models;
using FeedCraft.Domain.Services;
using FeedCraft.Infrastructure.Data;
using FeedCraft.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FeedCraft.Web.Controllers
{
    public class FeedController : Controller
    {
        private readonly IFeedOptimizationService _optimizer;
        private readonly FeedCraftDbContext _db;

        public FeedController(IFeedOptimizationService optimizer, FeedCraftDbContext db)
        {
            _optimizer = optimizer;
            _db = db;
        }

        [HttpGet]
        public IActionResult Index()
        {
            LoadSavedFormulations();
            return View(BuildModelFromLibrary());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Calculate(FeedFormulationViewModel model)
        {
            // Repair Ids / nutrient slots for anything the client added dynamically
            // before we validate or render.
            model.Normalize();

            if (!ValidateFormulation(model))
            {
                // Re-render the form (with validation messages) instead of running the
                // solver on invalid input — this is what prevents the zero-batch-size NaN.
                LoadSavedFormulations();
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
                    LoadSavedFormulations();
                    return View("Index", model);
                }
            }

            // Nothing to show (e.g. direct navigation) — start a fresh form.
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Persists the form exactly as submitted, together with the results it produces.
        /// The solver runs again here because the form posts inputs only — results are
        /// rendered output, not form fields — so re-solving is what guarantees the saved
        /// results actually belong to the saved inputs.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(FeedFormulationViewModel model)
        {
            model.Normalize();

            if (string.IsNullOrWhiteSpace(model.SaveName))
            {
                ModelState.AddModelError(nameof(model.SaveName),
                    "Enter a name before saving this formulation.");
            }

            if (!ValidateFormulation(model))
            {
                LoadSavedFormulations();
                return View("Index", model);
            }

            // Serialise the inputs *before* solving, so the snapshot's input half stays
            // free of result data.
            var inputsJson = JsonSerializer.Serialize(model);

            var result = _optimizer.OptimizeFeed(model);

            var saved = new SavedFormulation
            {
                Name = model.SaveName!.Trim(),
                CreatedAt = DateTime.Now,
                InputsJson = inputsJson,
                ResultsJson = JsonSerializer.Serialize(new FormulationResults
                {
                    IsSolved = result.IsSolved,
                    TotalCost = result.TotalCost,
                    OptimizedQuantities = result.OptimizedQuantities,
                    CalculatedNutrients = result.CalculatedNutrients,
                    ErrorMessage = result.ErrorMessage
                })
            };

            _db.SavedFormulations.Add(saved);
            _db.SaveChanges();

            TempData["FormulationResult"] = JsonSerializer.Serialize(result);
            TempData["LibraryMessage"] = $"Saved formulation \"{saved.Name}\".";
            return RedirectToAction(nameof(Result));
        }

        /// <summary>
        /// Rebuilds the form from a saved snapshot. Read-only by construction: the row is
        /// fetched with AsNoTracking() and the view model is not an entity, so loading a
        /// formulation can never write anything back.
        /// </summary>
        [HttpGet]
        public IActionResult Load(int id)
        {
            var saved = _db.SavedFormulations
                .AsNoTracking()
                .FirstOrDefault(s => s.Id == id);

            if (saved == null)
            {
                TempData["LibraryMessage"] = "That saved formulation no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            var model = JsonSerializer.Deserialize<FeedFormulationViewModel>(saved.InputsJson);
            if (model == null)
            {
                TempData["LibraryMessage"] = $"Could not read the snapshot for \"{saved.Name}\".";
                return RedirectToAction(nameof(Index));
            }

            var results = JsonSerializer.Deserialize<FormulationResults>(saved.ResultsJson);
            if (results != null)
            {
                model.IsSolved = results.IsSolved;
                model.TotalCost = results.TotalCost;
                model.OptimizedQuantities = results.OptimizedQuantities;
                model.CalculatedNutrients = results.CalculatedNutrients;
                model.ErrorMessage = results.ErrorMessage;
            }

            model.SaveName = saved.Name;

            // The snapshot already holds consistent Ids; this only re-sorts and gap-fills.
            model.Normalize();

            LoadSavedFormulations();
            ViewData["LoadedFormulation"] = $"{saved.Name} (saved {saved.CreatedAt:g})";
            return View("Index", model);
        }

        /// <summary>
        /// Shared guard for Calculate and Save. Returns true when the model is fit to solve.
        /// </summary>
        private bool ValidateFormulation(FeedFormulationViewModel model)
        {
            if (model.Ingredients == null || model.Ingredients.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Please add at least one ingredient.");
            }

            if (model.NutrientDefinitions == null || model.NutrientDefinitions.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Please define at least one nutrient.");
            }

            return ModelState.IsValid;
        }

        /// <summary>
        /// Reads the starting formulation out of the ingredient library.
        ///
        /// AsNoTracking() is essential: this becomes a detached working copy that the user
        /// edits freely in the browser, and Normalize() reassigns Ids for rows added
        /// client-side. Tracked entities would push those edits back into the library.
        /// </summary>
        private FeedFormulationViewModel BuildModelFromLibrary()
        {
            var model = new FeedFormulationViewModel
            {
                // Nutrient order drives the ingredient table's columns, so order both reads.
                NutrientDefinitions = _db.NutrientDefinitions
                    .AsNoTracking()
                    .OrderBy(n => n.Id)
                    .ToList(),

                Ingredients = _db.Ingredients
                    .AsNoTracking()
                    .Include(i => i.NutrientValues)
                    .OrderBy(i => i.Id)
                    .ToList(),

                Constraints = _db.NutrientConstraints
                    .AsNoTracking()
                    .OrderBy(c => c.NutrientDefinitionId)
                    .ToList(),

                BatchSize = 1000
            };

            model.Normalize();
            return model;
        }

        /// <summary>Fills the "Load formulation" dropdown, newest first.</summary>
        private void LoadSavedFormulations()
        {
            ViewData["SavedFormulations"] = _db.SavedFormulations
                .AsNoTracking()
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Select(s => new SavedFormulationSummary
                {
                    Id = s.Id,
                    Name = s.Name,
                    CreatedAt = s.CreatedAt
                })
                .ToList();
        }
    }
}
