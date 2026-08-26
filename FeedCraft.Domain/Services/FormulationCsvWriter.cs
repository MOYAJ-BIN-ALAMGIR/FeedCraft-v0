using System;
using System.Globalization;
using System.Text;
using FeedCraft.Domain.Models;

namespace FeedCraft.Domain.Services
{
    /// <summary>
    /// Renders a solved formulation as CSV: the mix, the nutrient analysis, and the two
    /// explanations (what the binding targets cost, and why an ingredient sits on a limit).
    ///
    /// <para>Pure and static — it reads the view model and returns a string, touching no solver,
    /// no database and no HTTP context. The controller runs the solve; this only reports it.</para>
    ///
    /// <para><b>No currency symbols anywhere.</b> Column headers say "Cost per kg", not "$/kg", so
    /// the export needs no attention when the app's currency changes.</para>
    ///
    /// <para><b>Numbers are formatted invariantly.</b> On a machine whose locale uses a decimal
    /// comma, <c>ToString("F2")</c> would emit <c>231,03</c> and split one value across two columns.
    /// Quoting would rescue the file, but a spreadsheet reading a comma-delimited export is better
    /// served by unambiguous numbers.</para>
    /// </summary>
    public static class FormulationCsvWriter
    {
        /// <summary>CRLF, as RFC 4180 specifies, rather than whatever the host platform uses.</summary>
        private const string NewLine = "\r\n";

        /// <summary>Kilograms in a tonne — the basis the sensitivity figures are quoted against.</summary>
        private const decimal KgPerTonne = 1000m;

        public static string Write(FeedFormulationViewModel model, DateTime exportedAt)
        {
            var sb = new StringBuilder();

            WriteHeader(sb, model, exportedAt);

            if (!model.IsSolved)
            {
                // Reached only if a caller exports an unsolved model anyway. Says so plainly
                // rather than emitting a file of empty sections that looks like a solved batch.
                Append(sb, "Result", "Not solved");
                if (!string.IsNullOrEmpty(model.ErrorMessage))
                {
                    Append(sb, "Error", model.ErrorMessage);
                }
                return sb.ToString();
            }

            decimal costTotal = SumLineCosts(model);

            sb.Append(NewLine);
            WriteMix(sb, model, costTotal);

            sb.Append(NewLine);
            WriteNutrients(sb, model);

            // Both explanations are omitted rather than emitted empty when there is nothing to
            // say — the same rule the page follows, so file and screen agree.
            if (model.SensitivityComputed && model.ShadowPrices.Count > 0)
            {
                sb.Append(NewLine);
                WriteSensitivity(sb, model);
            }

            if (model.ReducedCosts.Count > 0)
            {
                sb.Append(NewLine);
                WriteEconomics(sb, model);
            }

            return sb.ToString();
        }

        /// <summary>
        /// A filename that sorts by date and survives a formulation named
        /// <c>"Broiler starter, Aug 2026 (v2)"</c>.
        /// </summary>
        public static string SuggestFileName(string? formulationName, DateTime exportedAt)
        {
            string stamp = exportedAt.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            string slug = Slug(formulationName);

            return slug.Length == 0
                ? "feedcraft-mix-" + stamp + ".csv"
                : "feedcraft-" + slug + "-" + stamp + ".csv";
        }

        private static void WriteHeader(StringBuilder sb, FeedFormulationViewModel model, DateTime exportedAt)
        {
            Append(sb, "FeedCraft least-cost formulation");
            Append(sb, "Exported", exportedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

            if (!string.IsNullOrWhiteSpace(model.SaveName))
            {
                Append(sb, "Formulation", model.SaveName!.Trim());
            }

            Append(sb, "Batch size (kg)", Amount(model.BatchSize));

            if (model.IsSolved)
            {
                Append(sb, "Total cost", Money(model.TotalCost));

                if (model.BatchSize > 0.0)
                {
                    Append(sb, "Cost per kg", Rate(model.TotalCost / (decimal)model.BatchSize));
                }
            }
        }

        private static void WriteMix(StringBuilder sb, FeedFormulationViewModel model, decimal costTotal)
        {
            Append(sb, "Mix");
            Append(sb, "Ingredient", "Cost per kg", "Quantity (kg)", "Percent of batch",
                       "Line cost", "Percent of total cost", "Min inclusion percent", "Max inclusion percent");

            double quantityTotal = 0.0;

            foreach (var ingredient in model.Ingredients)
            {
                double quantity = model.OptimizedQuantities.TryGetValue(ingredient.Id, out var q) ? q : 0.0;
                quantityTotal += quantity;

                decimal lineCost = (decimal)quantity * ingredient.CostPerUnit;

                Append(sb,
                    ingredient.Name,
                    Rate(ingredient.CostPerUnit),
                    Amount(quantity),
                    Amount(PercentOfBatch(quantity, model.BatchSize)),
                    Money(lineCost),
                    Amount(Share(lineCost, costTotal)),
                    Amount(ingredient.MinInclusionPct ?? 0.0),
                    Amount(ingredient.MaxInclusionPct ?? 100.0));
            }

            // The totals row is a self-check as much as a summary: the line costs must add up to
            // the total cost printed in the header.
            Append(sb, "Total", string.Empty, Amount(quantityTotal),
                       Amount(PercentOfBatch(quantityTotal, model.BatchSize)),
                       Money(costTotal), Amount(costTotal > 0m ? 100.0 : 0.0),
                       string.Empty, string.Empty);
        }

        private static void WriteNutrients(StringBuilder sb, FeedFormulationViewModel model)
        {
            Append(sb, "Nutrient analysis");
            Append(sb, "Nutrient", "Unit", "Achieved", "Minimum target", "Maximum target", "Headroom");

            foreach (var nutrient in model.NutrientDefinitions)
            {
                double value = model.CalculatedNutrients.TryGetValue(nutrient.Id, out var v) ? v : 0.0;
                var bounds = model.FindConstraint(nutrient.Id);
                var headroom = NutrientHeadroom.From(nutrient, bounds, value);

                double? min = bounds?.MinValue;
                double? max = bounds?.MaxValue;

                Append(sb,
                    nutrient.Name,
                    nutrient.Unit,
                    Amount(value),
                    min.HasValue ? Amount(min.Value) : string.Empty,
                    max.HasValue ? Amount(max.Value) : string.Empty,
                    headroom.Describe(nutrient.Unit));
            }
        }

        private static void WriteSensitivity(StringBuilder sb, FeedFormulationViewModel model)
        {
            Append(sb, "Sensitivity - marginal cost of each binding target");
            Append(sb, "Nutrient", "Unit", "Binding at", "Bound value", "Cost per tonne per unit",
                       "Step", "Predicted saving per tonne");

            // Driven by the nutrient list, not the dictionary, so the rows keep the order of the
            // section above — the same reason the page iterates it that way.
            foreach (var nutrient in model.NutrientDefinitions)
            {
                if (!model.ShadowPrices.TryGetValue(nutrient.Id, out var price)) continue;

                string side = price.Side == BindingSide.Minimum ? "Minimum"
                            : price.Side == BindingSide.Maximum ? "Maximum"
                            : "Fixed";

                Append(sb,
                    nutrient.Name,
                    nutrient.Unit,
                    side,
                    Amount(price.BoundValue),
                    Rate(price.CostPerTonnePerUnit),
                    Amount(price.Delta, "0.####"),
                    Money(price.SavingPerTonne));
            }
        }

        private static void WriteEconomics(StringBuilder sb, FeedFormulationViewModel model)
        {
            Append(sb, "Ingredient economics - why an ingredient sits on an inclusion limit");
            Append(sb, "Ingredient", "Verdict", "Price per kg", "Off by per kg",
                       "Break-even price per kg", "Quantity (kg)");

            foreach (var ingredient in model.Ingredients)
            {
                if (!model.ReducedCosts.TryGetValue(ingredient.Id, out var economics)) continue;

                string verdict = economics.Verdict == InclusionVerdict.PricedOut ? "Priced out"
                               : economics.Verdict == InclusionVerdict.HeldInByMinimum ? "Held in by minimum"
                               : "Capped by maximum";

                Append(sb,
                    ingredient.Name,
                    verdict,
                    Rate(economics.CostPerUnit),
                    Rate(economics.PerKg),
                    // Blank rather than a negative number: an ingredient that would not be bought
                    // even free has no price at which it starts to pay off.
                    economics.CanEverPayOff ? Rate(economics.BreakEvenCost) : string.Empty,
                    Amount(economics.Quantity));
            }
        }

        private static decimal SumLineCosts(FeedFormulationViewModel model)
        {
            decimal total = 0m;

            foreach (var ingredient in model.Ingredients)
            {
                double quantity = model.OptimizedQuantities.TryGetValue(ingredient.Id, out var q) ? q : 0.0;
                total += (decimal)quantity * ingredient.CostPerUnit;
            }

            return total;
        }

        private static double PercentOfBatch(double quantity, double batchSize) =>
            batchSize > 0.0 ? quantity / batchSize * 100.0 : 0.0;

        private static double Share(decimal lineCost, decimal total) =>
            total > 0m ? (double)(lineCost / total * 100m) : 0.0;

        private static void Append(StringBuilder sb, params string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Escape(fields[i]));
            }

            sb.Append(NewLine);
        }

        /// <summary>Quantities, percentages and nutrient levels — two decimals unless told otherwise.</summary>
        private static string Amount(double value, string format = "F2") =>
            value.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>Money, to the cent.</summary>
        private static string Money(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);

        /// <summary>
        /// Per-unit prices, to four decimals. A shadow price of $0.0606 per kg rounds to $0.06 at
        /// two, and a break-even price is worth reading more precisely than that.
        /// </summary>
        private static string Rate(decimal value) => value.ToString("F4", CultureInfo.InvariantCulture);

        private static string Rate(double value) => value.ToString("F4", CultureInfo.InvariantCulture);

        private static string Escape(string? field)
        {
            string value = field ?? string.Empty;

            // A cell starting with =, + or @ is read as a formula by spreadsheet software, and
            // ingredient names are free text typed by the user. A leading apostrophe is the
            // standard defusing: spreadsheets take it as "this is text" and other readers see one
            // extra harmless character. A leading '-' is deliberately not touched — that is how a
            // genuine negative number starts.
            if (value.Length > 0 &&
                (value[0] == '=' || value[0] == '+' || value[0] == '@' || value[0] == '\t'))
            {
                value = "'" + value;
            }

            bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
                               || value.StartsWith(' ')
                               || value.EndsWith(' ');

            return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        /// <summary>Filename-safe form of a formulation name: lowercase, dashes, nothing exotic.</summary>
        private static string Slug(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            var sb = new StringBuilder();

            foreach (char c in name!.Trim())
            {
                if (char.IsLetterOrDigit(c) && c < 128)
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-')
                {
                    sb.Append('-');
                }

                if (sb.Length >= 40) break;
            }

            return sb.ToString().Trim('-');
        }
    }
}
