using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FeedCraft.Domain.Models
{
    /// <summary>
    /// A point-in-time snapshot of a whole formulation: the inputs the user entered
    /// and the results the solver produced, both stored as JSON.
    ///
    /// Deliberately a snapshot rather than a set of foreign keys into the ingredient
    /// library — editing or deleting an ingredient later must not silently rewrite
    /// (or break) a formulation that was already saved.
    /// </summary>
    public class SavedFormulation
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please give this formulation a name.")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        /// <summary>JSON of the <see cref="FeedFormulationViewModel"/> inputs, results stripped.</summary>
        public string InputsJson { get; set; } = string.Empty;

        /// <summary>JSON of a <see cref="FormulationResults"/>.</summary>
        public string ResultsJson { get; set; } = string.Empty;
    }

    /// <summary>
    /// The solver-output half of a <see cref="SavedFormulation"/>. Exists only as a
    /// serialization shape, so the snapshot keeps inputs and results in separate columns.
    /// </summary>
    public class FormulationResults
    {
        public bool IsSolved { get; set; }
        public decimal TotalCost { get; set; }
        public Dictionary<int, double> OptimizedQuantities { get; set; } = new Dictionary<int, double>();
        public Dictionary<int, double> CalculatedNutrients { get; set; } = new Dictionary<int, double>();
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
