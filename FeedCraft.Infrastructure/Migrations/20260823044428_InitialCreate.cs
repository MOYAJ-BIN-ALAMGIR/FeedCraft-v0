using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FeedCraft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CostPerUnit = table.Column<double>(type: "REAL", nullable: false),
                    MinInclusionPct = table.Column<double>(type: "REAL", nullable: true),
                    MaxInclusionPct = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NutrientDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsPercentage = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NutrientDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedFormulations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InputsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResultsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedFormulations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngredientNutrientValues",
                columns: table => new
                {
                    IngredientId = table.Column<int>(type: "INTEGER", nullable: false),
                    NutrientDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientNutrientValues", x => new { x.IngredientId, x.NutrientDefinitionId });
                    table.ForeignKey(
                        name: "FK_IngredientNutrientValues_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientNutrientValues_NutrientDefinitions_NutrientDefinitionId",
                        column: x => x.NutrientDefinitionId,
                        principalTable: "NutrientDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NutrientConstraints",
                columns: table => new
                {
                    NutrientDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    MinValue = table.Column<double>(type: "REAL", nullable: true),
                    MaxValue = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NutrientConstraints", x => x.NutrientDefinitionId);
                    table.ForeignKey(
                        name: "FK_NutrientConstraints_NutrientDefinitions_NutrientDefinitionId",
                        column: x => x.NutrientDefinitionId,
                        principalTable: "NutrientDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Ingredients",
                columns: new[] { "Id", "CostPerUnit", "MaxInclusionPct", "MinInclusionPct", "Name" },
                values: new object[,]
                {
                    { 1, 0.20000000000000001, 100.0, null, "Maize" },
                    { 2, 0.34999999999999998, 100.0, null, "Soybean Meal" },
                    { 3, 0.25, 100.0, null, "Mustard Meal" },
                    { 4, 0.90000000000000002, 100.0, null, "Vegetable Oil" }
                });

            migrationBuilder.InsertData(
                table: "NutrientDefinitions",
                columns: new[] { "Id", "IsPercentage", "Name", "Unit" },
                values: new object[,]
                {
                    { 1, true, "Crude Protein", "%" },
                    { 2, true, "Fat", "%" },
                    { 3, true, "Lysine", "%" },
                    { 4, true, "Ash", "%" },
                    { 5, false, "ME", "kcal/kg" }
                });

            migrationBuilder.InsertData(
                table: "IngredientNutrientValues",
                columns: new[] { "IngredientId", "NutrientDefinitionId", "Value" },
                values: new object[,]
                {
                    { 1, 1, 8.5 },
                    { 1, 2, 3.5 },
                    { 1, 3, 0.25 },
                    { 1, 4, 1.5 },
                    { 1, 5, 3300.0 },
                    { 2, 1, 44.0 },
                    { 2, 2, 1.5 },
                    { 2, 3, 2.7999999999999998 },
                    { 2, 4, 6.0 },
                    { 2, 5, 2200.0 },
                    { 3, 1, 35.0 },
                    { 3, 2, 8.0 },
                    { 3, 3, 1.5 },
                    { 3, 4, 7.0 },
                    { 3, 5, 2800.0 },
                    { 4, 1, 0.0 },
                    { 4, 2, 100.0 },
                    { 4, 3, 0.0 },
                    { 4, 4, 0.0 },
                    { 4, 5, 8800.0 }
                });

            migrationBuilder.InsertData(
                table: "NutrientConstraints",
                columns: new[] { "NutrientDefinitionId", "MaxValue", "MinValue" },
                values: new object[,]
                {
                    { 1, 24.0, 20.0 },
                    { 2, 10.0, 3.0 },
                    { 3, 1.5, 1.0 },
                    { 4, 8.0, 0.0 },
                    { 5, 3200.0, 2800.0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientNutrientValues_NutrientDefinitionId",
                table: "IngredientNutrientValues",
                column: "NutrientDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientNutrientValues");

            migrationBuilder.DropTable(
                name: "NutrientConstraints");

            migrationBuilder.DropTable(
                name: "SavedFormulations");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "NutrientDefinitions");
        }
    }
}
