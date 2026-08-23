using FeedCraft.Infrastructure.Data;
using FeedCraft.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace FeedCraft.Web.Controllers
{
    /// <summary>
    /// The knowledge base: published feeding specifications the user can read before
    /// formulating anything.
    ///
    /// Read-only and deliberately un-[Authorize]d — this is reference material, and there is
    /// nothing here worth making an account for. It also never touches the formulation form;
    /// copying a specification into the form is FeedController.LoadTemplate's job, so that the
    /// one-way direction of that copy is obvious from the controller layout alone.
    /// </summary>
    public class KnowledgeController : Controller
    {
        /// <summary>How much of the article the list page shows before trimming.</summary>
        private const int ExcerptLength = 160;

        private readonly FeedCraftDbContext _db;

        public KnowledgeController(FeedCraftDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Content is pulled because the excerpt is derived from it. Trimming in SQL would
            // save a few kilobytes across five rows and cost the word-boundary logic below,
            // which is not a trade worth making at this size.
            var entries = _db.KnowledgeEntries
                .AsNoTracking()
                .OrderBy(e => e.AnimalType)
                .ThenBy(e => e.Id)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    e.AnimalType,
                    e.Stage,
                    e.Content,
                    TargetCount = e.Targets.Count
                })
                .ToList();

            var model = entries
                .Select(e => new KnowledgeListItem
                {
                    Id = e.Id,
                    Title = e.Title,
                    AnimalType = e.AnimalType,
                    Stage = e.Stage,
                    Excerpt = BuildExcerpt(e.Content),
                    TargetCount = e.TargetCount
                })
                .ToList();

            return View(model);
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            var entry = _db.KnowledgeEntries
                .AsNoTracking()
                .Include(e => e.Targets)
                .FirstOrDefault(e => e.Id == id);

            if (entry == null)
            {
                return NotFound();
            }

            // Nutrient names live on NutrientDefinition, so the target rows are joined here
            // rather than in the view. Ordering by nutrient Id keeps the table in the same
            // order as the formulation page's nutrient list.
            var nutrients = _db.NutrientDefinitions
                .AsNoTracking()
                .ToDictionary(n => n.Id);

            var model = new KnowledgeDetailsViewModel
            {
                Entry = entry,
                Paragraphs = SplitParagraphs(entry.Content)
            };

            foreach (var target in entry.Targets.OrderBy(t => t.NutrientDefinitionId))
            {
                var row = new KnowledgeTargetRow
                {
                    NutrientDefinitionId = target.NutrientDefinitionId,
                    MinValue = target.MinValue,
                    MaxValue = target.MaxValue
                };

                if (nutrients.TryGetValue(target.NutrientDefinitionId, out var nutrient))
                {
                    row.NutrientName = nutrient.Name;
                    row.Unit = nutrient.Unit;
                }

                if (!string.IsNullOrWhiteSpace(target.Note))
                {
                    // Identical notes share one footnote — both min-only energy rows carry the
                    // same explanation, and printing it twice would just be noise.
                    var existing = model.Footnotes.IndexOf(target.Note);
                    if (existing < 0)
                    {
                        model.Footnotes.Add(target.Note);
                        existing = model.Footnotes.Count - 1;
                    }

                    row.FootnoteNumber = existing + 1;
                }

                model.Targets.Add(row);
            }

            return View(model);
        }

        /// <summary>
        /// Splits an article body into paragraphs on blank lines. Tolerates both \n and \r\n so
        /// the result does not depend on how the seed file happened to be checked out.
        /// </summary>
        private static List<string> SplitParagraphs(string content)
        {
            return (content ?? string.Empty)
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();
        }

        /// <summary>
        /// One-line description for the list page: the article's first sentence, or its opening
        /// words trimmed at a word boundary when that sentence is long.
        /// </summary>
        private static string BuildExcerpt(string content)
        {
            var first = SplitParagraphs(content).FirstOrDefault() ?? string.Empty;

            var stop = first.IndexOf(". ", System.StringComparison.Ordinal);
            if (stop > 0 && stop + 1 <= ExcerptLength)
            {
                return first[..(stop + 1)];
            }

            if (first.Length <= ExcerptLength)
            {
                return first;
            }

            var cut = first.LastIndexOf(' ', ExcerptLength);
            return string.Concat(first.AsSpan(0, cut > 0 ? cut : ExcerptLength), "…");
        }
    }
}
