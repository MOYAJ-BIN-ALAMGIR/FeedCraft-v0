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
            LoadFormPickers();
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

            LoadFormPickers();
            ViewData["LoadedFormulation"] = $"{saved.Name} (saved {saved.CreatedAt:g})";
            return View("Index", model);
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
        /// The copy is strictly one-way: the KnowledgeEntry is read AsNoTracking() and nothing
        /// here writes to the database, so editing the form afterwards cannot alter the
        /// reference data.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LoadTemplate(FeedFormulationViewModel model, int templateId)
        {
            // Repair Ids/slots for anything added client-side before reading the nutrient
            // list, same as Calculate does.
            model.Normalize();

            var entry = _db.KnowledgeEntries
                .AsNoTracking()
                .Include(e => e.Targets)
                .FirstOrDefault(e => e.Id == templateId);

            if (entry == null)
            {
                TempData["LibraryMessage"] = "That knowledge base template no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            var targets = entry.Targets.ToDictionary(t => t.NutrientDefinitionId);

            // Rebuilt by walking NutrientDefinitions, so the list comes out in nutrient order
            // and stays index-aligned with the table the view renders.
            //
            // A nutrient the template does not mention is left *unbounded* rather than keeping
            // whatever was previously typed. Otherwise a leftover bound could make the mix
            // infeasible for a reason that is nowhere on screen — the user would see "no
            // solution" for a constraint the template never asked for.
            model.Constraints = model.NutrientDefinitions
                .Select(nutrient => new NutrientConstraint
                {
                    NutrientDefinitionId = nutrient.Id,
                    MinValue = targets.TryGetValue(nutrient.Id, out var t) ? t.MinValue : null,
                    MaxValue = targets.TryGetValue(nutrient.Id, out var u) ? u.MaxValue : null
                })
                .ToList();

            // A template can also target a nutrient this form does not have. Naming those is
            // better than silently adding nutrient rows the user did not ask for.
            var formNutrientIds = model.NutrientDefinitions.Select(n => n.Id).ToHashSet();
            var unmatched = entry.Targets
                .Where(t => !formNutrientIds.Contains(t.NutrientDefinitionId))
                .Select(t => t.NutrientDefinitionId)
                .ToList();

            var appliedCount = entry.Targets.Count - unmatched.Count;
            var message = $"Loaded targets from \"{entry.Title}\" — {appliedCount} nutrient" +
                          $"{(appliedCount == 1 ? "" : "s")} set.";

            if (unmatched.Count > 0)
            {
                var names = _db.NutrientDefinitions
                    .AsNoTracking()
                    .Where(n => unmatched.Contains(n.Id))
                    .Select(n => n.Name)
                    .ToList();

                message += $" This template also targets {string.Join(", ", names)}, which " +
                           "this formulation does not track — add the nutrient below and load " +
                           "the template again to apply it.";
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

            ViewData["KnowledgeTemplates"] = _db.KnowledgeEntries
                .AsNoTracking()
                .OrderBy(e => e.AnimalType)
                .ThenBy(e => e.Id)
                .Select(e => new KnowledgeTemplateSummary
                {
                    Id = e.Id,
                    Title = e.Title
                })
                .ToList();
        }
    }
}
