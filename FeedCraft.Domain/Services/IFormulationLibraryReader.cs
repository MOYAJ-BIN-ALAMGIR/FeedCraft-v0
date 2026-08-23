using System.Collections.Generic;
using FeedCraft.Domain.Models;

namespace FeedCraft.Domain.Services
{
    /// <summary>
    /// Reads the reference data every formulation screen starts from: the ingredient library
    /// itself, plus the contents of the two pickers above the form.
    ///
    /// It exists because more than one page now needs that starting state. The formulation
    /// page and the experiment page must begin from the *same* library read — the ingredient
    /// order alone determines the nutrient column layout and the cost of the resulting mix —
    /// so duplicating the query would be duplicating the answer.
    /// </summary>
    public interface IFormulationLibraryReader
    {
        /// <summary>
        /// A fresh, detached working copy of the ingredient library, ready to be edited in the
        /// browser. Callers get a new instance every time; two panels on one page must never
        /// share one.
        /// </summary>
        FeedFormulationViewModel BuildStartingFormulation();

        /// <summary>Saved snapshots, newest first.</summary>
        List<SavedFormulationSummary> ListSavedFormulations();

        /// <summary>Knowledge-base entries usable as constraint templates.</summary>
        List<KnowledgeTemplateSummary> ListKnowledgeTemplates();

        /// <summary>
        /// Rebuilds one saved snapshot into an editable formulation, results included.
        ///
        /// Returns null when no such snapshot exists. Read-only by construction: the row is
        /// fetched with AsNoTracking() and what comes back is a view model, not an entity, so
        /// loading a formulation can never write anything back.
        /// </summary>
        FormulationSnapshot? LoadSnapshot(int id);
    }
}
