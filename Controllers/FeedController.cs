using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FeedCraft.Domain.Models;
using FeedCraft.Domain.Services;
using FeedCraft.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FeedCraft.Web.Controllers
{
    public class FeedController : Controller
    {
        private readonly IFeedOptimizationService _optimizer;
        private readonly IFormulationLibraryReader _library;
        private readonly IKnowledgeTemplateApplier _templates;
        private readonly FeedCraftDbContext _db;

        public FeedController(
            IFeedOptimizationService optimizer,
            IFormulationLibraryReader library,
            IKnowledgeTemplateApplier templates,
            FeedCraftDbContext db)
        {
            _optimizer = optimizer;
            _library = library;
            _templates = templates;
            _db = db;
        }

        [HttpGet]
        public IActionResult Index()
        {
            LoadFormPickers();
            return View(_library.BuildStartingFormulation());
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
                LoadFormPickers();
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
                    LoadFormPickers();
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
                LoadFormPickers();
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
        /// Rebuilds the form from a saved snapshot. Read-only by construction — see
        /// <see cref="IFormulationLibraryReader.LoadSnapshot"/>, which /Experiment shares.
        /// </summary>
        [HttpGet]
        public IActionResult Load(int id)
        {
            var snapshot = _library.LoadSnapshot(id);

            if (snapshot == null)
            {
                TempData["LibraryMessage"] = "That saved formulation no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            if (snapshot.Formulation == null)
            {
                TempData["LibraryMessage"] = $"Could not read the snapshot for \"{snapshot.Name}\".";
                return RedirectToAction(nameof(Index));
            }

            LoadFormPickers();
            ViewData["LoadedFormulation"] = $"{snapshot.Name} (saved {snapshot.CreatedAt:g})";
            return View("Index", snapshot.Formulation);
        }

        /// <summary>
        /// Copies a knowledge-base entry's nutrient targets into the constraint fields of the
        /// form the user is currently editing.
        ///
        /// A POST rather than a GET, and it binds the whole view model, because it has to
        /// preserve everything else on the form — edited ingredient costs, added rows, batch
        /// size. (Contrast Load above, which is a GET precisely because it replaces the form
        /// wholesale from a snapshot.)
        ///
        /// The copy itself lives in <see cref="IKnowledgeTemplateApplier"/>, shared with
        /// /Experiment. It is strictly one-way: nothing there writes to the database, so editing
        /// the form afterwards cannot alter the reference data.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LoadTemplate(FeedFormulationViewModel model, int templateId)
        {
            // Repair Ids/slots for anything added client-side before reading the nutrient
            // list, same as Calculate does.
            model.Normalize();

            var message = _templates.Apply(model, templateId);

            if (message == null)
            {
                TempData["LibraryMessage"] = "That knowledge base template no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            // The nutrient inputs are rendered by tag helpers, and those read ModelState in
            // preference to the model itself. Without this the values just posted would win and
            // the loaded targets would never appear on screen — the banner would claim a
            // template had been applied while the fields still showed the old numbers.
            //
            // Only the Constraints entries are dropped. Everything else must keep re-rendering
            // exactly what the user typed, including text that failed to bind: clearing all of
            // ModelState would quietly rewrite a cost of "0.2o" as 0 while their attention was
            // on the nutrient table.
            foreach (var key in ModelState.Keys
                         .Where(k => k.StartsWith("Constraints", StringComparison.Ordinal))
                         .ToList())
            {
                ModelState.Remove(key);
            }

            ViewData["LoadedTemplate"] = message;
            LoadFormPickers();
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
        /// Fills both dropdowns above the form: saved formulations (newest first) and
        /// knowledge-base templates.
        ///
        /// They are loaded together in one helper on purpose — every path that re-renders
        /// Index calls this, so a new picker cannot be forgotten on one of them and silently
        /// vanish from, say, the validation-failure re-render.
        /// </summary>
        private void LoadFormPickers()
        {
            ViewData["SavedFormulations"] = _library.ListSavedFormulations();
            ViewData["KnowledgeTemplates"] = _library.ListKnowledgeTemplates();
        }
    }
}
