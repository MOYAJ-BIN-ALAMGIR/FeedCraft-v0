using System.Collections.Generic;

namespace FeedCraft.Domain.Models
{
    /// <summary>
    /// One article in the knowledge base: a published feeding specification for a given
    /// animal type and production stage, plus the nutrient target ranges that go with it.
    ///
    /// The targets are reference data. Loading a template on the formulation page copies
    /// them one-way into the working <see cref="FeedFormulationViewModel.Constraints"/>;
    /// nothing the user does to the form writes back here.
    /// </summary>
    public class KnowledgeEntry
    {
        public int Id { get; set; }

        /// <summary>Species grouping used to sort the list page, e.g. "Broiler", "Layer", "Cattle".</summary>
        public string AnimalType { get; set; } = string.Empty;

        /// <summary>Production stage within the animal type, e.g. "Starter (0-10 days)".</summary>
        public string Stage { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// The article body. Plain text, blank-line separated into paragraphs by the view —
        /// deliberately not HTML, so it can be rendered through Razor's encoding @ and never
        /// through Html.Raw.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Forward navigation only, matching <see cref="Ingredient.NutrientValues"/>. Safe here
        /// because a KnowledgeEntry is never serialised into the formulation view model, so
        /// there is no cyclic-graph risk of the kind that keeps DealerListing navigation-free.
        /// </summary>
        public List<KnowledgeEntryTarget> Targets { get; set; } = new();
    }
}
