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

            SeedLibrary(modelBuilder);
            SeedRoles(modelBuilder);
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
    }
}
