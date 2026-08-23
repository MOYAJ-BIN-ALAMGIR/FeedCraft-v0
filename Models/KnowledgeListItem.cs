namespace FeedCraft.Web.Models;

/// <summary>
/// One row on the /Knowledge list page. The Excerpt is derived from the entry's Content
/// rather than stored as its own column, so there is no second copy of the prose to keep
/// in step with the article.
/// </summary>
public class KnowledgeListItem
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string AnimalType { get; set; } = string.Empty;

    public string Stage { get; set; } = string.Empty;

    /// <summary>First sentence of the article, trimmed at a word boundary.</summary>
    public string Excerpt { get; set; } = string.Empty;

    public int TargetCount { get; set; }
}
