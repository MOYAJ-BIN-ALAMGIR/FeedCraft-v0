using FeedCraft.Domain.Models;

namespace FeedCraft.Web.Models;

/// <summary>
/// Everything /Knowledge/Details/{id} renders: the article itself plus its target ranges
/// already joined to nutrient names and units, so the view does no lookups of its own.
/// </summary>
public class KnowledgeDetailsViewModel
{
    public KnowledgeEntry Entry { get; set; } = new();

    public List<KnowledgeTargetRow> Targets { get; set; } = new();

    /// <summary>
    /// Paragraphs of <see cref="KnowledgeEntry.Content"/>, split on blank lines. The view
    /// emits each through Razor's encoding @ — the content is never treated as HTML.
    /// </summary>
    public List<string> Paragraphs { get; set; } = new();

    /// <summary>
    /// The distinct footnotes carried by the target rows, in the order they first appear.
    /// A row's marker is its index here plus one.
    /// </summary>
    public List<string> Footnotes { get; set; } = new();
}

/// <summary>One line of the nutrient target table.</summary>
public class KnowledgeTargetRow
{
    public int NutrientDefinitionId { get; set; }

    public string NutrientName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public double? MinValue { get; set; }

    public double? MaxValue { get; set; }

    /// <summary>1-based footnote marker, or null when this row carries no note.</summary>
    public int? FootnoteNumber { get; set; }
}
