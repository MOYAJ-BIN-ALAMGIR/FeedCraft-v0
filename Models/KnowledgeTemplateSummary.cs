namespace FeedCraft.Web.Models;

/// <summary>
/// One entry in the "Load Template" dropdown on the formulation page. Excludes Content so
/// filling the dropdown never pulls five article bodies into memory.
/// </summary>
public class KnowledgeTemplateSummary
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
}
