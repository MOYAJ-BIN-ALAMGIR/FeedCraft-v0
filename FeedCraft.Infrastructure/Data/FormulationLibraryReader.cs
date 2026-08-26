using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FeedCraft.Domain.Models;
using FeedCraft.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace FeedCraft.Infrastructure.Data
{
    /// <summary>
    /// EF Core implementation of <see cref="IFormulationLibraryReader"/>.
    ///
    /// Every read here is AsNoTracking(), and that is not an optimisation: what comes back is a
    /// detached working copy the user edits freely in the browser, and
    /// <see cref="FeedFormulationViewModel.Normalize"/> reassigns Ids for rows added
    /// client-side. Tracked entities would push those edits straight back into the library.
    /// </summary>
    public class FormulationLibraryReader : IFormulationLibraryReader
    {
        private readonly FeedCraftDbContext _db;

        public FormulationLibraryReader(FeedCraftDbContext db)
        {
            _db = db;
        }

        public FeedFormulationViewModel BuildStartingFormulation()
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

        public List<SavedFormulationSummary> ListSavedFormulations() =>
            _db.SavedFormulations
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

        public List<KnowledgeTemplateSummary> ListKnowledgeTemplates() =>
            _db.KnowledgeEntries
                .AsNoTracking()
                .OrderBy(e => e.AnimalType)
                .ThenBy(e => e.Id)
                .Select(e => new KnowledgeTemplateSummary
                {
                    Id = e.Id,
                    Title = e.Title
                })
                .ToList();

        public FormulationSnapshot? LoadSnapshot(int id)
        {
            var saved = _db.SavedFormulations
                .AsNoTracking()
                .FirstOrDefault(s => s.Id == id);

            if (saved == null)
            {
                return null;
            }

            var snapshot = new FormulationSnapshot
            {
                Id = saved.Id,
                Name = saved.Name,
                CreatedAt = saved.CreatedAt
            };

            var model = JsonSerializer.Deserialize<FeedFormulationViewModel>(saved.InputsJson);
            if (model == null)
            {
                // Found, but unreadable. The caller says so rather than showing an empty form.
                return snapshot;
            }

            var results = JsonSerializer.Deserialize<FormulationResults>(saved.ResultsJson);
            if (results != null)
            {
                model.IsSolved = results.IsSolved;
                model.TotalCost = results.TotalCost;
                model.OptimizedQuantities = results.OptimizedQuantities;
                model.CalculatedNutrients = results.CalculatedNutrients;
                model.ShadowPrices = results.ShadowPrices;
                model.SensitivityComputed = results.SensitivityComputed;
                model.ReducedCosts = results.ReducedCosts;
                model.ErrorMessage = results.ErrorMessage;
            }

            model.SaveName = saved.Name;

            // The snapshot already holds consistent Ids; this only re-sorts and gap-fills.
            model.Normalize();

            snapshot.Formulation = model;
            return snapshot;
        }
    }
}
