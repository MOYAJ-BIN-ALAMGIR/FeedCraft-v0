using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FeedCraft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnimalType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Stage = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeEntryTargets",
                columns: table => new
                {
                    KnowledgeEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    NutrientDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    MinValue = table.Column<double>(type: "REAL", nullable: true),
                    MaxValue = table.Column<double>(type: "REAL", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeEntryTargets", x => new { x.KnowledgeEntryId, x.NutrientDefinitionId });
                    table.ForeignKey(
                        name: "FK_KnowledgeEntryTargets_KnowledgeEntries_KnowledgeEntryId",
                        column: x => x.KnowledgeEntryId,
                        principalTable: "KnowledgeEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeEntryTargets_NutrientDefinitions_NutrientDefinitionId",
                        column: x => x.NutrientDefinitionId,
                        principalTable: "NutrientDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "KnowledgeEntries",
                columns: new[] { "Id", "AnimalType", "Content", "Stage", "Title" },
                values: new object[,]
                {
                    { 1, "Broiler", "The starter phase covers hatch to roughly ten days of age, when the chick's growth rate relative to its own body weight is the highest it will ever be. Feed intake is still small, so every gram has to be nutrient-dense: the specification is driven by amino acid concentration rather than by energy.\n\nPublished targets for this phase sit at 22-24% crude protein and 2,950-3,050 kcal/kg metabolisable energy, with digestible lysine at 1.25-1.45%. The protein-to-energy ratio matters more than either figure on its own - diluting protein while holding energy produces fatty birds with poor early frame development.\n\nStarter feed is normally offered as a crumb. Physical form is not something this tool models, but it affects intake enough that a least-cost mix which looks correct on paper can still underperform if it is fed as a coarse mash.", "Starter (0-10 days)", "Broiler Starter (0-10 days)" },
                    { 2, "Broiler", "From about eleven to twenty-four days the bird is still growing quickly but is now eating enough that nutrient density can be relaxed slightly. Crude protein steps down to 20-22% while energy steps up to 3,000-3,100 kcal/kg.\n\nDigestible lysine of 1.10-1.25% remains the amino acid that limits growth, and the other essential amino acids are usually specified as a ratio to it - methionine plus cystine at about 75% of lysine, threonine at about 65%. This application tracks whichever nutrients you define, so those ratios can be added as extra nutrient rows on the formulation page if you want to formulate against them directly.\n\nFat is given a wider band here (4-9%) because supplemental oil is the cheapest way to lift energy once the grain fraction is already near its practical maximum.", "Grower (11-24 days)", "Broiler Grower (11-24 days)" },
                    { 3, "Broiler", "The finisher phase runs from around twenty-five days to slaughter. Growth is slowing, feed conversion is what drives profit, and the ration shifts decisively towards energy: 3,050-3,200 kcal/kg against a reduced 18-20% crude protein.\n\nDigestible lysine falls to 0.95-1.10%. This is also the phase where withdrawal requirements apply - any additive carrying a mandated clearance period has to be out of the feed before the birds are processed.\n\nBecause energy rather than protein is the binding requirement here, the finisher is the ration whose least-cost answer is most sensitive to the oil price. That makes it a useful example when discussing marginal values: a small change in the cost of the energy source moves the entire solution.", "Finisher (25 days to market)", "Broiler Finisher (25 days to market)" },
                    { 4, "Layer", "A commercial layer at peak production - roughly 26 to 45 weeks - converts feed into an egg on almost every day of the period. The ration is built around a steady daily nutrient intake rather than around growth, so commercial specifications are usually quoted per hen per day as well as per kilogram of feed.\n\nPublished targets are 16-18% crude protein, 2,750-2,900 kcal/kg metabolisable energy and 0.80-0.95% digestible lysine. Energy is deliberately held lower than in any broiler ration: a hen eats to her energy requirement, so an over-dense feed reduces intake enough to leave her short of protein and calcium.\n\nCalcium is what really distinguishes a layer ration from every other feed described here. A hen in lay needs 3.5-4.2% dietary calcium, most of it from a coarse particulate source such as limestone or oyster shell so that it dissolves slowly overnight while the shell is being formed. The four ingredients seeded in this application supply almost none, so a limestone or dicalcium phosphate row has to be added to the ingredient library before a genuine layer ration can be formulated.\n\nThe ash ceiling in the table below is set at 14% rather than the 8% used for broilers precisely because a real layer ration carries that mineral load.", "Peak production (26-45 weeks)", "Layer, Peak Production" },
                    { 5, "Cattle", "This entry covers the concentrate portion of a lactating dairy cow's diet, not the total ration. A cow in milk takes most of her dry matter as forage; the concentrate is the fraction formulated to close the gap between what that forage supplies and what production requires. The figures below are therefore not comparable with the poultry entries above.\n\nTypical targets for a dairy concentrate are 16-18% crude protein and 2,600-2,800 kcal/kg metabolisable energy, with fat held to 2-6%. That fat ceiling is a rumen constraint rather than a nutritional one: above roughly 6-7% total dietary fat, unprotected oil begins to inhibit fibre-digesting bacteria and depresses both intake and milk fat percentage.\n\nNo lysine target is listed, and the omission is deliberate. Rumen microbes synthesise amino acids from degradable nitrogen, so the amino acid profile of the feed itself is a poor predictor of what the cow actually absorbs. Constraining a ruminant concentrate to a dietary lysine minimum the way you would for a broiler is not meaningful. Loading this template consequently clears the lysine bounds on the formulation form rather than setting them.\n\nEffective fibre matters as much as any number in the table, and this tool does not model it. A concentrate that meets every target below can still cause rumen acidosis if it is fed without adequate long forage.", "Lactating concentrate", "Dairy Cattle, Lactating Concentrate" }
                });

            migrationBuilder.InsertData(
                table: "KnowledgeEntryTargets",
                columns: new[] { "KnowledgeEntryId", "NutrientDefinitionId", "MaxValue", "MinValue", "Note" },
                values: new object[,]
                {
                    { 1, 1, 24.0, 22.0, null },
                    { 1, 2, 8.0, 3.0, null },
                    { 1, 3, 1.45, 1.25, null },
                    { 1, 4, 8.0, null, null },
                    { 1, 5, 3050.0, 2950.0, null },
                    { 2, 1, 22.0, 20.0, null },
                    { 2, 2, 9.0, 4.0, null },
                    { 2, 3, 1.25, 1.1000000000000001, null },
                    { 2, 4, 8.0, null, null },
                    { 2, 5, 3100.0, 3000.0, null },
                    { 3, 1, 20.0, 18.0, null },
                    { 3, 2, 10.0, 4.0, null },
                    { 3, 3, 1.1000000000000001, 0.94999999999999996, null },
                    { 3, 4, 8.0, null, null },
                    { 3, 5, 3200.0, 3050.0, null },
                    { 4, 1, 18.0, 16.0, null },
                    { 4, 2, 8.0, 3.0, null },
                    { 4, 3, 0.94999999999999996, 0.80000000000000004, null },
                    { 4, 4, 14.0, null, null },
                    { 4, 5, null, 2750.0, "No upper bound is loaded: the four seeded ingredients cannot fall below about 3,005 kcal/kg at this protein level. Add a fibrous ingredient such as rice bran or wheat bran to reach the published window." },
                    { 5, 1, 18.0, 16.0, null },
                    { 5, 2, 6.0, 2.0, null },
                    { 5, 4, 10.0, null, null },
                    { 5, 5, null, 2600.0, "No upper bound is loaded: the four seeded ingredients cannot fall below about 3,005 kcal/kg at this protein level. Add a fibrous ingredient such as rice bran or wheat bran to reach the published window." }
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEntries_AnimalType",
                table: "KnowledgeEntries",
                column: "AnimalType");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEntryTargets_NutrientDefinitionId",
                table: "KnowledgeEntryTargets",
                column: "NutrientDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeEntryTargets");

            migrationBuilder.DropTable(
                name: "KnowledgeEntries");
        }
    }
}
