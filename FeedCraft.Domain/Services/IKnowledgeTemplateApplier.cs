using FeedCraft.Domain.Models;

namespace FeedCraft.Domain.Services
{
    /// <summary>
    /// Copies a knowledge-base entry's nutrient targets into a formulation's constraint fields.
    ///
    /// One implementation shared by /Feed and /Experiment, so "load the Broiler Starter targets"
    /// means exactly the same thing on both pages — including the awkward edges, like what
    /// happens to a nutrient the template does not mention.
    /// </summary>
    public interface IKnowledgeTemplateApplier
    {
        /// <summary>
        /// Rewrites <paramref name="model"/>'s constraints from the template's targets and
        /// returns the message describing what was applied.
        ///
        /// Returns null when the template no longer exists, in which case the model is left
        /// untouched. Strictly one-way: nothing is written to the knowledge base.
        /// </summary>
        string? Apply(FeedFormulationViewModel model, int templateId);
    }
}
