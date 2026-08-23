using Microsoft.AspNetCore.Mvc;
using FeedCraft.Domain.Models;
using FeedCraft.Domain.Services;
using FeedCraft.Web.Models;
using System;
using System.Linq;

namespace FeedCraft.Web.Controllers
{
    /// <summary>
    /// Side-by-side comparison of two formulations.
    ///
    /// Two design points worth stating, because both are easy to get wrong:
    ///
    /// One form, one submit. "Independently" in the spec means two separate linear programs, not
    /// two HTTP requests. With a form per panel, solving A would re-render the page and wipe B's
    /// result. One form posts both panels, the optimizer runs twice on two separate model
    /// instances, and the summary is therefore always built from results that came out of the
    /// same submit — it cannot show a stale A against a fresh B.
    ///
    /// No Post-Redirect-Get. /Feed redirects through TempData, but the app registers no session,
    /// so TempData is cookie-backed and two full formulations would blow past the ~4 KB cookie
    /// limit — silently. Solve renders directly instead. The cost is that F5 re-submits; results
    /// here are display-only, so nothing is lost.
    ///
    /// Nothing on this page writes to the database. There is no save action, no SaveChanges call,
    /// and every read goes through the AsNoTracking() readers.
    /// </summary>
    public class ExperimentController : Controller
    {
        private readonly IFeedOptimizationService _optimizer;
        private readonly IFormulationLibraryReader _library;
        private readonly IKnowledgeTemplateApplier _templates;
        private readonly IFormulationComparer _comparer;

        public ExperimentController(
            IFeedOptimizationService optimizer,
            IFormulationLibraryReader library,
            IKnowledgeTemplateApplier templates,
            IFormulationComparer comparer)
        {
            _optimizer = optimizer;
            _library = library;
            _templates = templates;
            _comparer = comparer;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new ExperimentViewModel
            {
                // Two separate calls, deliberately. Handing the same instance to both panels
                // would make an edit in A show up in B.
                PanelA = new ExperimentPanelViewModel
                {
                    Label = "A",
                    Formulation = _library.BuildStartingFormulation()
                },
                PanelB = new ExperimentPanelViewModel
                {
                    Label = "B",
                    Formulation = _library.BuildStartingFormulation()
                }
            };

            LoadFormPickers();
            return View(model);
        }

        /// <summary>
        /// Solves both panels and compares them. Each panel is judged on its own errors, so a
        /// mistyped cost in B does not deny the user A's answer.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Solve(ExperimentViewModel model)
        {
            PrepareForRender(model);

            // Repair Ids / nutrient slots for anything added client-side before validating,
            // exactly as FeedController.Calculate does.
            model.PanelA.Formulation.Normalize();
            model.PanelB.Formulation.Normalize();

            if (ValidatePanel(model.PanelA))
            {
                _optimizer.OptimizeFeed(model.PanelA.Formulation);
            }

            if (ValidatePanel(model.PanelB))
            {
                _optimizer.OptimizeFeed(model.PanelB.Formulation);
            }

            model.Diff = _comparer.Compare(model.PanelA.Formulation, model.PanelB.Formulation);

            LoadFormPickers();
            return View("Index", model);
        }

        /// <summary>
        /// Applies a knowledge-base template to one panel's constraints, leaving that panel's
        /// ingredients and batch size — and the whole of the other panel — alone.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LoadTemplate(ExperimentViewModel model, string panel)
        {
            PrepareForRender(model);

            var target = PanelFor(model, panel);
            if (target == null)
            {
                return RedirectToAction(nameof(Index));
            }

            target.Formulation.Normalize();

            if (!target.TemplateId.HasValue)
            {
                target.LoadedNotice = "Pick a template to load into this panel.";
            }
            else
            {
                var message = _templates.Apply(target.Formulation, target.TemplateId.Value);

                if (message == null)
                {
                    target.LoadedNotice = "That knowledge base template no longer exists.";
                }
                else
                {
                    target.LoadedNotice = message;

                    // Tag helpers read ModelState in preference to the model, so the constraints
                    // just posted would otherwise win and the loaded targets would never reach
                    // the screen.
                    //
                    // Scoped twice over: only Constraints, and only this panel's. Everything else
                    // must keep re-rendering what the user typed, and the other panel must not be
                    // touched at all.
                    ClearModelStateFor($"Panel{target.Label}.Formulation.Constraints");
                }
            }

            LoadFormPickers();
            return View("Index", model);
        }

        /// <summary>
        /// Replaces one panel with a saved snapshot, results included.
        ///
        /// Read-only: the snapshot is fetched AsNoTracking() and rebuilt into a view model, so
        /// editing the panel afterwards cannot change the saved record.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LoadSaved(ExperimentViewModel model, string panel)
        {
            PrepareForRender(model);

            var target = PanelFor(model, panel);
            if (target == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var snapshot = target.SavedFormulationId.HasValue
                ? _library.LoadSnapshot(target.SavedFormulationId.Value)
                : null;

            if (snapshot == null)
            {
                target.LoadedNotice = "Pick a saved formulation to load into this panel.";
            }
            else if (snapshot.Formulation == null)
            {
                target.LoadedNotice = $"Could not read the snapshot for \"{snapshot.Name}\".";
            }
            else
            {
                target.Formulation = snapshot.Formulation;
                target.LoadedNotice = $"Loaded \"{snapshot.Name}\" (saved {snapshot.CreatedAt:g}). " +
                                      "Editing this panel does not change the saved copy.";

                // The whole panel was replaced, so the whole panel's ModelState has to go —
                // and only that panel's.
                ClearModelStateFor($"Panel{target.Label}.Formulation");
            }

            LoadFormPickers();
            return View("Index", model);
        }

        /// <summary>
        /// Restores what the form does not post: the panel labels, and any panel the binder left
        /// empty. Also drops a stale diff — the two results on screen belong to the previous
        /// submit, so any summary of them is out of date the moment a panel changes.
        /// </summary>
        private static void PrepareForRender(ExperimentViewModel model)
        {
            model.PanelA ??= new ExperimentPanelViewModel();
            model.PanelB ??= new ExperimentPanelViewModel();
            model.PanelA.Label = "A";
            model.PanelB.Label = "B";
            model.PanelA.Formulation ??= new FeedFormulationViewModel();
            model.PanelB.Formulation ??= new FeedFormulationViewModel();
            model.Diff = null;
        }

        private static ExperimentPanelViewModel? PanelFor(ExperimentViewModel model, string panel) =>
            string.Equals(panel, "A", StringComparison.OrdinalIgnoreCase) ? model.PanelA
            : string.Equals(panel, "B", StringComparison.OrdinalIgnoreCase) ? model.PanelB
            : null;

        /// <summary>
        /// Guard for one panel. Returns true when that panel is fit to solve.
        ///
        /// Messages name the panel: with two formulations on screen, "please add at least one
        /// ingredient" tells the user nothing about where to look.
        /// </summary>
        private bool ValidatePanel(ExperimentPanelViewModel panel)
        {
            var ok = true;
            var formulation = panel.Formulation;

            if (formulation.Ingredients == null || formulation.Ingredients.Count == 0)
            {
                ModelState.AddModelError(string.Empty,
                    $"Panel {panel.Label}: please add at least one ingredient.");
                ok = false;
            }

            if (formulation.NutrientDefinitions == null || formulation.NutrientDefinitions.Count == 0)
            {
                ModelState.AddModelError(string.Empty,
                    $"Panel {panel.Label}: please define at least one nutrient.");
                ok = false;
            }

            // Binding and annotation failures are keyed by field name, so they can be attributed
            // to a panel — which is what lets the other panel still be solved. (This is also why
            // the batch-size guard still works here: a 0 fails its Range attribute under
            // PanelX.Formulation.BatchSize, and the solver is never reached with it.)
            var prefix = $"Panel{panel.Label}.";
            if (ModelState.Any(entry =>
                    entry.Key.StartsWith(prefix, StringComparison.Ordinal) &&
                    entry.Value != null && entry.Value.Errors.Count > 0))
            {
                ok = false;
            }

            return ok;
        }

        /// <summary>
        /// Drops the ModelState entries under one field prefix, so a panel this action rewrote
        /// re-renders from the model instead of from what was posted. Never ModelState.Clear():
        /// that would also discard text that failed to bind, quietly turning a cost of "0.2o"
        /// into 0 while the user was looking elsewhere.
        /// </summary>
        private void ClearModelStateFor(string fieldPrefix)
        {
            foreach (var key in ModelState.Keys
                         .Where(k => k.StartsWith(fieldPrefix, StringComparison.Ordinal))
                         .ToList())
            {
                ModelState.Remove(key);
            }
        }

        /// <summary>
        /// Both panels' pickers read the same two lists, so they are loaded once per request.
        /// </summary>
        private void LoadFormPickers()
        {
            ViewData["SavedFormulations"] = _library.ListSavedFormulations();
            ViewData["KnowledgeTemplates"] = _library.ListKnowledgeTemplates();
        }
    }
}
