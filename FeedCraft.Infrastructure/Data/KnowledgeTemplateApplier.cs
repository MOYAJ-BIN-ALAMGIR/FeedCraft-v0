using System.Collections.Generic;
using System.Linq;
using FeedCraft.Domain.Models;
using FeedCraft.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace FeedCraft.Infrastructure.Data
{
    /// <summary>
    /// EF Core implementation of <see cref="IKnowledgeTemplateApplier"/>. Both reads are
    /// AsNoTracking(): applying a template must never be able to edit the reference data it
    /// copies from.
    /// </summary>
    public class KnowledgeTemplateApplier : IKnowledgeTemplateApplier
    {
        private readonly FeedCraftDbContext _db;

        public KnowledgeTemplateApplier(FeedCraftDbContext db)
        {
            _db = db;
        }

        public string? Apply(FeedFormulationViewModel model, int templateId)
        {
            var entry = _db.KnowledgeEntries
                .AsNoTracking()
                .Include(e => e.Targets)
                .FirstOrDefault(e => e.Id == templateId);

            if (entry == null)
            {
                return null;
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

            return message;
        }
    }
}
