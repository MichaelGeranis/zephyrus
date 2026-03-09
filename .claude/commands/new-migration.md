# /new-migration

Add an EF Core migration for Zephyrus.

Migration description: $ARGUMENTS
(Use PascalCase description — e.g. AddFeatureTable, AddArtifactApprovedAtColumn)

## Instructions

Create or update EF Core entities and generate the migration.
All persistence code lives in Zephyrus.Infrastructure/Persistence/.
Read ARCHITECTURE.md and the current DbContext before making changes.

---

## Steps to Follow

### 1. Update or create the entity in Zephyrus.Core/Entities/
Entities are plain C# classes — no EF Core attributes in Core.
Use data annotations only if absolutely necessary; prefer Fluent API.

```csharp
namespace Zephyrus.Core.Entities;

public class {EntityName}
{
    public Guid Id { get; init; } = Guid.NewGuid();
    // Add properties using C# 12 primary constructor or init-only setters
    // Use DateTimeOffset (not DateTime) for all timestamps
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
```

### 2. Add Fluent API configuration in Zephyrus.Infrastructure/Persistence/Configurations/
```csharp
namespace Zephyrus.Infrastructure.Persistence.Configurations;

public class {EntityName}Configuration : IEntityTypeConfiguration<{EntityName}>
{
    public void Configure(EntityTypeBuilder<{EntityName}> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever(); // We generate GUIDs in code

        // Add column configurations, indexes, relationships
        // Example:
        builder.Property(x => x.Status)
            .HasConversion<string>()  // Store enums as strings, not ints
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.CreatedAt);
    }
}
```

### 3. Register in ZephyrusDbContext if new entity
```csharp
// In Zephyrus.Infrastructure/Persistence/ZephyrusDbContext.cs
public DbSet<{EntityName}> {EntityName}s => Set<{EntityName}>();
```

### 4. Generate the migration
```bash
cd src/Zephyrus.Infrastructure
dotnet ef migrations add {YYYYMMDD}_{Description} \
  --project Zephyrus.Infrastructure.csproj \
  --startup-project ../Zephyrus.Api/Zephyrus.Api.csproj \
  --output-dir Persistence/Migrations
```

### 5. Review the generated migration file
Check the Up() and Down() methods make sense.
Ensure Down() fully reverts Up().

---

## Conventions

| Rule | Detail |
|------|--------|
| All IDs | `Guid`, generated in application code (not DB) |
| All timestamps | `DateTimeOffset`, always UTC |
| Enums stored as | Strings (not integers) — readable in raw SQL |
| Soft deletes | Add `DeletedAt DateTimeOffset?` — never hard delete domain entities |
| String lengths | Always set `HasMaxLength()` — no unbounded strings except `text` fields |
| Indexes | Add on all FK columns and frequently queried columns |
| Migration naming | `{YYYYMMDD}_{PascalCaseDescription}` |

---

## Checklist Before Finishing

- [ ] Entity defined in Zephyrus.Core with no EF attributes
- [ ] Fluent configuration in Zephyrus.Infrastructure/Persistence/Configurations/
- [ ] DbSet registered in ZephyrusDbContext
- [ ] Migration generated and reviewed
- [ ] Down() method correctly reverts Up()
- [ ] No string columns without HasMaxLength()
- [ ] Enums stored as strings
- [ ] Timestamps use DateTimeOffset not DateTime
