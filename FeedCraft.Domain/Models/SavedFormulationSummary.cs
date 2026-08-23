namespace FeedCraft.Domain.Models
{
    /// <summary>
    /// One entry in the "Load formulation" dropdown. Deliberately excludes the two JSON
    /// snapshot columns so listing saved formulations never pulls their full payloads.
    /// </summary>
    public class SavedFormulationSummary
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
