using System;

namespace FeedCraft.Domain.Models
{
    /// <summary>
    /// One saved formulation, rebuilt from its stored JSON and ready to edit.
    ///
    /// <see cref="Formulation"/> is null when the stored snapshot could not be read — a real
    /// possibility for rows written by an older version of the app. That is a different outcome
    /// from "no such snapshot" (a null result), and the two get different messages on screen.
    /// </summary>
    public class FormulationSnapshot
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        /// <summary>Null when the stored inputs could not be deserialised.</summary>
        public FeedFormulationViewModel? Formulation { get; set; }
    }
}
