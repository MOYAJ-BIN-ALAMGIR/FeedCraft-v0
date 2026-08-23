using FeedCraft.Domain.Models;

namespace FeedCraft.Web.Models
{
    /// <summary>
    /// One side of the experiment page: a formulation, plus the picker state that belongs to the
    /// screen rather than to the formulation itself.
    ///
    /// The picker ids deliberately live here and not on <see cref="FeedFormulationViewModel"/>:
    /// that class is serialised whole into SavedFormulation.InputsJson, so anything added to it
    /// ends up in every future snapshot.
    /// </summary>
    public class ExperimentPanelViewModel
    {
        /// <summary>"A" or "B". Display only, and the value the load actions route on.</summary>
        public string Label { get; set; } = string.Empty;

        public FeedFormulationViewModel Formulation { get; set; } = new FeedFormulationViewModel();

        /// <summary>Current selection in this panel's knowledge-base template picker.</summary>
        public int? TemplateId { get; set; }

        /// <summary>Current selection in this panel's saved-formulation picker.</summary>
        public int? SavedFormulationId { get; set; }

        /// <summary>What was just loaded into this panel, if anything. Shown in the card.</summary>
        public string? LoadedNotice { get; set; }
    }

    /// <summary>
    /// The experiment page: two independent formulations and, once both have been solved, the
    /// difference between them.
    /// </summary>
    public class ExperimentViewModel
    {
        public ExperimentPanelViewModel PanelA { get; set; } = new ExperimentPanelViewModel { Label = "A" };
        public ExperimentPanelViewModel PanelB { get; set; } = new ExperimentPanelViewModel { Label = "B" };

        /// <summary>Null until Solve has run. Never stale: it is only ever built from the two
        /// results produced by the same submit.</summary>
        public FormulationDiff? Diff { get; set; }
    }
}
