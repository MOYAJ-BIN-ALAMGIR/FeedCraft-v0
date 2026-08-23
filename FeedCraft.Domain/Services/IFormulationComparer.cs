using FeedCraft.Domain.Models;

namespace FeedCraft.Domain.Services
{
    /// <summary>
    /// Compares two solved formulations. Pure: no database, no HTTP, no formatting —
    /// two view models in, one <see cref="FormulationDiff"/> out.
    /// </summary>
    public interface IFormulationComparer
    {
        FormulationDiff Compare(FeedFormulationViewModel panelA, FeedFormulationViewModel panelB);
    }
}
