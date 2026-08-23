using FeedCraft.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FeedCraft.Infrastructure.Data
{
    /// <summary>
    /// Persistence for the ingredient/nutrient library plus saved formulation snapshots.
    ///
    /// Two deliberate constraints shape the mapping below:
    ///
    /// 1. **No reverse navigation properties.** FeedController round-trips the formulation
    ///    view model through JsonSerializer (TempData), and a child->parent back-reference
    ///    would make that graph cyclic and throw at runtime. Every relationship here is
    ///    configured with an argument-less WithOne()/WithMany(), so EF gets its foreign keys
    ///    without any reverse navigation being required.
    ///
    /// 2. **The formulation form is never tracked.** FeedFormulationViewModel is not an
    ///    entity and appears in no DbSet — it is a detached working copy read via
    ///    AsNoTracking(). Its Normalize() reassigns Ids for rows added client-side, which
    ///    would corrupt library rows if it were ever attached.
    ///
    /// Since Step 6 this also hosts ASP.NET Core Identity (one database, one migration chain).
    /// DealerListing's foreign key to AspNetUsers is configured *here* rather than as a
    /// navigation property on the entity, so FeedCraft.Domain needs no Identity reference.
    /// </summary>
    public class FeedCraftDbContext : IdentityDbContext<IdentityUser>
    {
        /// <summary>
        /// The one role in the app. Kept as a const next to the seed that creates it so the
        /// string in [Authorize(Roles = ...)], in User.IsInRole checks and in the seeded
        /// NormalizedName can never drift apart.
        /// </summary>
        public const string DealerRole = "Dealer";

        public FeedCraftDbContext(DbContextOptions<FeedCraftDbContext> options)
            : base(options)
        {
        }

        public DbSet<Ingredient> Ingredients => Set<Ingredient>();
        public DbSet<NutrientDefinition> NutrientDefinitions => Set<NutrientDefinition>();
        public DbSet<IngredientNutrientValue> IngredientNutrientValues => Set<IngredientNutrientValue>();
        public DbSet<NutrientConstraint> NutrientConstraints => Set<NutrientConstraint>();
        public DbSet<SavedFormulation> SavedFormulations => Set<SavedFormulation>();
        public DbSet<DealerListing> DealerListings => Set<DealerListing>();
        public DbSet<KnowledgeEntry> KnowledgeEntries => Set<KnowledgeEntry>();
        public DbSet<KnowledgeEntryTarget> KnowledgeEntryTargets => Set<KnowledgeEntryTarget>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Must run first: this is what maps the seven AspNet* Identity tables. Without it
            // the base class buys you nothing and the Identity schema silently never appears.
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<NutrientDefinition>(entity =>
            {
                entity.ToTable("NutrientDefinitions");
                entity.HasKey(n => n.Id);
                entity.Property(n => n.Id).ValueGeneratedOnAdd();
                entity.Property(n => n.Name).IsRequired().HasMaxLength(100);
                entity.Property(n => n.Unit).IsRequired().HasMaxLength(20);
            });

            modelBuilder.Entity<Ingredient>(entity =>
            {
                entity.ToTable("Ingredients");
                entity.HasKey(i => i.Id);
                entity.Property(i => i.Id).ValueGeneratedOnAdd();
                entity.Property(i => i.Name).IsRequired().HasMaxLength(100);

                // SQLite has no decimal type. Left alone, EF stores decimal as TEXT and warns
                // that ordering and comparison will misbehave. Storing as REAL is safe here:
                // the solver already casts this to double (FeedOptimizationService), and a
                // 2-dp value round-trips exactly (0.20m -> 0.2d -> 0.20m).
                entity.Property(i => i.CostPerUnit).HasConversion<double>();

                // Forward navigation only. WithOne() with no argument tells EF "there is no
                // reverse navigation" — which is what keeps the JSON graph acyclic.
                entity.HasMany(i => i.NutrientValues)
                      .WithOne()
                      .HasForeignKey(v => v.IngredientId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<IngredientNutrientValue>(entity =>
            {
                entity.ToTable("IngredientNutrientValues");

                // The class has no Id property; (IngredientId, NutrientDefinitionId) is the
                // natural key and enforces one reading per ingredient per nutrient.
                entity.HasKey(v => new { v.IngredientId, v.NutrientDefinitionId });

                // No navigation on either side of this leg.
                entity.HasOne<NutrientDefinition>()
                      .WithMany()
                      .HasForeignKey(v => v.NutrientDefinitionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<NutrientConstraint>(entity =>
            {
                entity.ToTable("NutrientConstraints");

                // No Id on the class: one default min/max row per nutrient, so the nutrient
                // is the key. ValueGeneratedNever() is required — otherwise EF treats this
                // int primary key as an identity column and overwrites the seeded values.
                entity.HasKey(c => c.NutrientDefinitionId);
                entity.Property(c => c.NutrientDefinitionId).ValueGeneratedNever();

                entity.HasOne<NutrientDefinition>()
                      .WithMany()
                      .HasForeignKey(c => c.NutrientDefinitionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SavedFormulation>(entity =>
            {
                entity.ToTable("SavedFormulations");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Id).ValueGeneratedOnAdd();
                entity.Property(s => s.Name).IsRequired().HasMaxLength(200);
                entity.Property(s => s.InputsJson).IsRequired();
                entity.Property(s => s.ResultsJson).IsRequired();
            });

            modelBuilder.Entity<DealerListing>(entity =>
            {
                entity.ToTable("DealerListings");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.Id).ValueGeneratedOnAdd();
                entity.Property(l => l.DealerUserId).IsRequired();
                entity.Property(l => l.IngredientName).IsRequired().HasMaxLength(100);
                entity.Property(l => l.ContactInfo).IsRequired().HasMaxLength(300);

                // Same reasoning as Ingredient.CostPerUnit above: SQLite has no decimal type.
                entity.Property(l => l.Price).HasConversion<double>();

                // The Market page's only search predicate.
                entity.HasIndex(l => l.IngredientName);

                // The link to AspNetUsers lives here, not on the entity — no navigation on
                // either side, matching the discipline used for the nutrient relationships.
                // Cascade so deleting a dealer's account takes their listings with it.
                entity.HasOne<IdentityUser>()
                      .WithMany()
                      .HasForeignKey(l => l.DealerUserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<KnowledgeEntry>(entity =>
            {
                entity.ToTable("KnowledgeEntries");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.AnimalType).IsRequired().HasMaxLength(60);
                entity.Property(e => e.Stage).IsRequired().HasMaxLength(80);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(160);

                // No length cap — this is the article body, not a label.
                entity.Property(e => e.Content).IsRequired();

                // The list page sorts by animal type, so index it.
                entity.HasIndex(e => e.AnimalType);

                // Argument-less WithOne(): the target has no back-reference to its entry,
                // same discipline as Ingredient -> IngredientNutrientValue above.
                entity.HasMany(e => e.Targets)
                      .WithOne()
                      .HasForeignKey(t => t.KnowledgeEntryId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<KnowledgeEntryTarget>(entity =>
            {
                entity.ToTable("KnowledgeEntryTargets");

                // No Id on the class; one target row per nutrient per entry.
                entity.HasKey(t => new { t.KnowledgeEntryId, t.NutrientDefinitionId });

                entity.Property(t => t.Note).HasMaxLength(300);

                entity.HasOne<NutrientDefinition>()
                      .WithMany()
                      .HasForeignKey(t => t.NutrientDefinitionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            SeedLibrary(modelBuilder);
            SeedRoles(modelBuilder);
            SeedKnowledgeBase(modelBuilder);
        }

        /// <summary>
        /// The Dealer role, created by the migration so a fresh clone can register a dealer
        /// immediately with no manual setup step.
        ///
        /// Every value here is a hard-coded literal on purpose. HasData must be deterministic:
        /// a Guid.NewGuid() for Id or ConcurrencyStamp would make each `dotnet ef migrations add`
        /// emit a spurious UpdateData for this row forever.
        /// </summary>
        private static void SeedRoles(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IdentityRole>().HasData(new IdentityRole
            {
                Id = "9f3c1d2e-7a54-4b18-9c60-2f8e5b1a4d73",
                Name = DealerRole,

                // Not cosmetic. UserManager.AddToRoleAsync and User.IsInRole both resolve a role
                // by its *normalized* (upper-case) name. Seed this lower-case or null and role
                // assignment quietly finds nothing.
                NormalizedName = "DEALER",
                ConcurrencyStamp = "0c7d4a91-5e26-49b3-8f11-6ad38c705b42"
            });
        }

        /// <summary>
        /// The out-of-the-box demo dataset, seeded through the migration so a fresh clone
        /// still solves correctly. These values are the ones the app shipped with before
        /// persistence existed, and the regression baseline ($231.03 on a 1000 kg batch)
        /// depends on them being exact.
        /// </summary>
        private static void SeedLibrary(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NutrientDefinition>().HasData(
                new NutrientDefinition { Id = 1, Name = "Crude Protein", Unit = "%",       IsPercentage = true  },
                new NutrientDefinition { Id = 2, Name = "Fat",           Unit = "%",       IsPercentage = true  },
                new NutrientDefinition { Id = 3, Name = "Lysine",        Unit = "%",       IsPercentage = true  },
                new NutrientDefinition { Id = 4, Name = "Ash",           Unit = "%",       IsPercentage = true  },
                new NutrientDefinition { Id = 5, Name = "ME",            Unit = "kcal/kg", IsPercentage = false }
            );

            modelBuilder.Entity<Ingredient>().HasData(
                new Ingredient { Id = 1, Name = "Maize",         CostPerUnit = 0.20m, MinInclusionPct = null, MaxInclusionPct = 100 },
                new Ingredient { Id = 2, Name = "Soybean Meal",  CostPerUnit = 0.35m, MinInclusionPct = null, MaxInclusionPct = 100 },
                new Ingredient { Id = 3, Name = "Mustard Meal",  CostPerUnit = 0.25m, MinInclusionPct = null, MaxInclusionPct = 100 },
                // Oil satisfies high energy requirements while letting other ingredients meet protein.
                new Ingredient { Id = 4, Name = "Vegetable Oil", CostPerUnit = 0.90m, MinInclusionPct = null, MaxInclusionPct = 100 }
            );

            // Seeded as their own rows with explicit foreign keys — HasData cannot seed a
            // populated Ingredient.NutrientValues graph.
            // Readings are in nutrient-definition order: CP, Fat, Lysine, Ash, ME (Ids 1-5).
            var readings = new[]
            {
                (IngredientId: 1, Values: new[] {  8.5,   3.5, 0.25, 1.5, 3300.0 }),
                (IngredientId: 2, Values: new[] { 44.0,   1.5, 2.80, 6.0, 2200.0 }),
                (IngredientId: 3, Values: new[] { 35.0,   8.0, 1.50, 7.0, 2800.0 }),
                (IngredientId: 4, Values: new[] {  0.0, 100.0, 0.00, 0.0, 8800.0 })
            };

            var nutrientValues = readings
                .SelectMany(reading => reading.Values.Select((value, index) => new IngredientNutrientValue
                {
                    IngredientId = reading.IngredientId,
                    NutrientDefinitionId = index + 1, // nutrient Ids are 1-based, seeded in order
                    Value = value
                }))
                .ToList();

            modelBuilder.Entity<IngredientNutrientValue>().HasData(nutrientValues);

            modelBuilder.Entity<NutrientConstraint>().HasData(
                new NutrientConstraint { NutrientDefinitionId = 1, MinValue = 20.0,   MaxValue = 24.0   },
                new NutrientConstraint { NutrientDefinitionId = 2, MinValue = 3.0,    MaxValue = 10.0   },
                new NutrientConstraint { NutrientDefinitionId = 3, MinValue = 1.0,    MaxValue = 1.5    },
                new NutrientConstraint { NutrientDefinitionId = 4, MinValue = 0.0,    MaxValue = 8.0    },
                new NutrientConstraint { NutrientDefinitionId = 5, MinValue = 2800.0, MaxValue = 3200.0 }
            );
        }

        /// <summary>
        /// Seeds the study entries behind /Knowledge and the "Load Template" dropdown.
        ///
        /// Every figure here is a published feeding specification, and the article text states
        /// the published range in full. Where the loadable bound deliberately differs from the
        /// published one, the target row carries a Note that the details page renders as a
        /// footnote — see energyFloorNote below.
        /// </summary>
        private static void SeedKnowledgeBase(ModelBuilder modelBuilder)
        {
            // Paragraphs are joined with an explicit "\n\n" rather than written as one
            // multi-line literal: a raw string literal would bake this file's CRLF line
            // endings into the seeded value, so flipping git's autocrlf setting would surface
            // as a spurious data diff on the next migrations add.
            static string Article(params string[] paragraphs) => string.Join("\n\n", paragraphs);

            modelBuilder.Entity<KnowledgeEntry>().HasData(
                new KnowledgeEntry
                {
                    Id = 1,
                    AnimalType = "Broiler",
                    Stage = "Starter (0-10 days)",
                    Title = "Broiler Starter (0-10 days)",
                    Content = Article(
                        "The starter phase covers hatch to roughly ten days of age, when the chick's growth rate relative to its own body weight is the highest it will ever be. Feed intake is still small, so every gram has to be nutrient-dense: the specification is driven by amino acid concentration rather than by energy.",
                        "Published targets for this phase sit at 22-24% crude protein and 2,950-3,050 kcal/kg metabolisable energy, with digestible lysine at 1.25-1.45%. The protein-to-energy ratio matters more than either figure on its own - diluting protein while holding energy produces fatty birds with poor early frame development.",
                        "Starter feed is normally offered as a crumb. Physical form is not something this tool models, but it affects intake enough that a least-cost mix which looks correct on paper can still underperform if it is fed as a coarse mash.")
                },
                new KnowledgeEntry
                {
                    Id = 2,
                    AnimalType = "Broiler",
                    Stage = "Grower (11-24 days)",
                    Title = "Broiler Grower (11-24 days)",
                    Content = Article(
                        "From about eleven to twenty-four days the bird is still growing quickly but is now eating enough that nutrient density can be relaxed slightly. Crude protein steps down to 20-22% while energy steps up to 3,000-3,100 kcal/kg.",
                        "Digestible lysine of 1.10-1.25% remains the amino acid that limits growth, and the other essential amino acids are usually specified as a ratio to it - methionine plus cystine at about 75% of lysine, threonine at about 65%. This application tracks whichever nutrients you define, so those ratios can be added as extra nutrient rows on the formulation page if you want to formulate against them directly.",
                        "Fat is given a wider band here (4-9%) because supplemental oil is the cheapest way to lift energy once the grain fraction is already near its practical maximum.")
                },
                new KnowledgeEntry
                {
                    Id = 3,
                    AnimalType = "Broiler",
                    Stage = "Finisher (25 days to market)",
                    Title = "Broiler Finisher (25 days to market)",
                    Content = Article(
                        "The finisher phase runs from around twenty-five days to slaughter. Growth is slowing, feed conversion is what drives profit, and the ration shifts decisively towards energy: 3,050-3,200 kcal/kg against a reduced 18-20% crude protein.",
                        "Digestible lysine falls to 0.95-1.10%. This is also the phase where withdrawal requirements apply - any additive carrying a mandated clearance period has to be out of the feed before the birds are processed.",
                        "Because energy rather than protein is the binding requirement here, the finisher is the ration whose least-cost answer is most sensitive to the oil price. That makes it a useful example when discussing marginal values: a small change in the cost of the energy source moves the entire solution.")
                },
                new KnowledgeEntry
                {
                    Id = 4,
                    AnimalType = "Layer",
                    Stage = "Peak production (26-45 weeks)",
                    Title = "Layer, Peak Production",
                    Content = Article(
                        "A commercial layer at peak production - roughly 26 to 45 weeks - converts feed into an egg on almost every day of the period. The ration is built around a steady daily nutrient intake rather than around growth, so commercial specifications are usually quoted per hen per day as well as per kilogram of feed.",
                        "Published targets are 16-18% crude protein, 2,750-2,900 kcal/kg metabolisable energy and 0.80-0.95% digestible lysine. Energy is deliberately held lower than in any broiler ration: a hen eats to her energy requirement, so an over-dense feed reduces intake enough to leave her short of protein and calcium.",
                        "Calcium is what really distinguishes a layer ration from every other feed described here. A hen in lay needs 3.5-4.2% dietary calcium, most of it from a coarse particulate source such as limestone or oyster shell so that it dissolves slowly overnight while the shell is being formed. The four ingredients seeded in this application supply almost none, so a limestone or dicalcium phosphate row has to be added to the ingredient library before a genuine layer ration can be formulated.",
                        "The ash ceiling in the table below is set at 14% rather than the 8% used for broilers precisely because a real layer ration carries that mineral load.")
                },
                new KnowledgeEntry
                {
                    Id = 5,
                    AnimalType = "Cattle",
                    Stage = "Lactating concentrate",
                    Title = "Dairy Cattle, Lactating Concentrate",
                    Content = Article(
                        "This entry covers the concentrate portion of a lactating dairy cow's diet, not the total ration. A cow in milk takes most of her dry matter as forage; the concentrate is the fraction formulated to close the gap between what that forage supplies and what production requires. The figures below are therefore not comparable with the poultry entries above.",
                        "Typical targets for a dairy concentrate are 16-18% crude protein and 2,600-2,800 kcal/kg metabolisable energy, with fat held to 2-6%. That fat ceiling is a rumen constraint rather than a nutritional one: above roughly 6-7% total dietary fat, unprotected oil begins to inhibit fibre-digesting bacteria and depresses both intake and milk fat percentage.",
                        "No lysine target is listed, and the omission is deliberate. Rumen microbes synthesise amino acids from degradable nitrogen, so the amino acid profile of the feed itself is a poor predictor of what the cow actually absorbs. Constraining a ruminant concentrate to a dietary lysine minimum the way you would for a broiler is not meaningful. Loading this template consequently clears the lysine bounds on the formulation form rather than setting them.",
                        "Effective fibre matters as much as any number in the table, and this tool does not model it. A concentrate that meets every target below can still cause rumen acidosis if it is fed without adequate long forage.")
                }
            );

            // Explanation attached to the ME row of the two rations whose published energy
            // ceiling is unreachable with the seeded ingredient list. Kept as one constant so
            // both rows read identically on screen.
            const string energyFloorNote =
                "No upper bound is loaded: the four seeded ingredients cannot fall below about " +
                "3,005 kcal/kg at this protein level. Add a fibrous ingredient such as rice bran " +
                "or wheat bran to reach the published window.";

            // Seeded as their own rows with explicit foreign keys, exactly as
            // IngredientNutrientValue is — HasData cannot seed a populated Targets graph.
            // Nutrient Ids come from SeedLibrary: 1 Crude Protein %, 2 Fat %, 3 Lysine %,
            // 4 Ash %, 5 ME kcal/kg.
            modelBuilder.Entity<KnowledgeEntryTarget>().HasData(
                // 1 — Broiler Starter
                new KnowledgeEntryTarget { KnowledgeEntryId = 1, NutrientDefinitionId = 1, MinValue = 22.0,   MaxValue = 24.0   },
                new KnowledgeEntryTarget { KnowledgeEntryId = 1, NutrientDefinitionId = 2, MinValue = 3.0,    MaxValue = 8.0    },
                new KnowledgeEntryTarget { KnowledgeEntryId = 1, NutrientDefinitionId = 3, MinValue = 1.25,   MaxValue = 1.45   },
                new KnowledgeEntryTarget { KnowledgeEntryId = 1, NutrientDefinitionId = 4, MinValue = null,   MaxValue = 8.0    },
                new KnowledgeEntryTarget { KnowledgeEntryId = 1, NutrientDefinitionId = 5, MinValue = 2950.0, MaxValue = 3050.0 },

                // 2 — Broiler Grower
                new KnowledgeEntryTarget { KnowledgeEntryId = 2, NutrientDefinitionId = 1, MinValue = 20.0,   MaxValue = 22.0   },
                new KnowledgeEntryTarget { KnowledgeEntryId = 2, NutrientDefinitionId = 2, MinValue = 4.0,    MaxValue = 9.0    },
                new KnowledgeEntryTarget { KnowledgeEntryId = 2, NutrientDefinitionId = 3, MinValue = 1.10,   MaxValue = 1.25   },
                new KnowledgeEntryTarget { KnowledgeEntryId = 2, NutrientDefinitionId = 4, MinValue = null,   MaxValue = 8.0    },
                new KnowledgeEntryTarget { KnowledgeEntryId = 2, NutrientDefinitionId = 5, MinValue = 3000.0, MaxValue = 3100.0 },

                // 3 — Broiler Finisher
                new KnowledgeEntryTarget { KnowledgeEntryId = 3, NutrientDefinitionId = 1, MinValue = 18.0,   MaxValue = 20.0   },
                new KnowledgeEntryTarget { KnowledgeEntryId = 3, NutrientDefinitionId = 2, MinValue = 4.0,    MaxValue = 10.0   },
                new KnowledgeEntryTarget { KnowledgeEntryId = 3, NutrientDefinitionId = 3, MinValue = 0.95,   MaxValue = 1.10   },
                new KnowledgeEntryTarget { KnowledgeEntryId = 3, NutrientDefinitionId = 4, MinValue = null,   MaxValue = 8.0    },
                new KnowledgeEntryTarget { KnowledgeEntryId = 3, NutrientDefinitionId = 5, MinValue = 3050.0, MaxValue = 3200.0 },

                // 4 — Layer at peak. ME is min-only; the article states the published 2,750-2,900.
                new KnowledgeEntryTarget { KnowledgeEntryId = 4, NutrientDefinitionId = 1, MinValue = 16.0,   MaxValue = 18.0   },
                new KnowledgeEntryTarget { KnowledgeEntryId = 4, NutrientDefinitionId = 2, MinValue = 3.0,    MaxValue = 8.0    },
                new KnowledgeEntryTarget { KnowledgeEntryId = 4, NutrientDefinitionId = 3, MinValue = 0.80,   MaxValue = 0.95   },
                new KnowledgeEntryTarget { KnowledgeEntryId = 4, NutrientDefinitionId = 4, MinValue = null,   MaxValue = 14.0   },
                new KnowledgeEntryTarget { KnowledgeEntryId = 4, NutrientDefinitionId = 5, MinValue = 2750.0, MaxValue = null, Note = energyFloorNote },

                // 5 — Dairy concentrate. No lysine row at all: see the article for why.
                new KnowledgeEntryTarget { KnowledgeEntryId = 5, NutrientDefinitionId = 1, MinValue = 16.0,   MaxValue = 18.0   },
                new KnowledgeEntryTarget { KnowledgeEntryId = 5, NutrientDefinitionId = 2, MinValue = 2.0,    MaxValue = 6.0    },
                new KnowledgeEntryTarget { KnowledgeEntryId = 5, NutrientDefinitionId = 4, MinValue = null,   MaxValue = 10.0   },
                new KnowledgeEntryTarget { KnowledgeEntryId = 5, NutrientDefinitionId = 5, MinValue = 2600.0, MaxValue = null, Note = energyFloorNote }
            );
        }
    }
}
