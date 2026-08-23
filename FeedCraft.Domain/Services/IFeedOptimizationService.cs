using FeedCraft.Domain.Models;

namespace FeedCraft.Domain.Services
{
    /// <summary>
    /// Least-cost feed formulation. Takes a fully-populated view model (ingredients,
    /// nutrient definitions, constraints, batch size) and returns the same instance with
    /// the optimization results filled in (or an error message if it could not be solved).
    /// </summary>
    public interface IFeedOptimizationService
    {
        FeedFormulationViewModel OptimizeFeed(FeedFormulationViewModel model);
    }
}
