namespace FeedCraft.Domain.Models
{
    /// <summary>
    /// One nutrient target range belonging to a <see cref="KnowledgeEntry"/>.
    ///
    /// Shaped field-for-field like <see cref="NutrientConstraint"/> (nullable Min/Max in the
    /// nutrient's own unit) so that loading a template is a straight copy with no conversion.
    /// Like <see cref="IngredientNutrientValue"/> it has no Id of its own — the key is
    /// (KnowledgeEntryId, NutrientDefinitionId) — and no reverse navigation.
    /// </summary>
    public class KnowledgeEntryTarget
    {
        public int KnowledgeEntryId { get; set; }
        public int NutrientDefinitionId { get; set; }

        /// <summary>Lower bound, or null to leave the nutrient unbounded below.</summary>
        public double? MinValue { get; set; }

        /// <summary>Upper bound, or null to leave the nutrient unbounded above.</summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// Optional footnote shown beside the range on the details page — used where the
        /// loadable bound deliberately differs from the published one, so the reason is on
        /// the page rather than buried in a seed comment.
        /// </summary>
        public string? Note { get; set; }
    }
}
