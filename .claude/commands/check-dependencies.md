# /check-dependencies

Verify no layer is violating the Clean Architecture dependency rule across Zephyrus.

## Instructions

Scan all .csproj files and source files in the solution.
Report every violation of the dependency rule.
Read ARCHITECTURE.md for the full rules before scanning.

---

## The Dependency Rule

```
ALLOWED:
  Zephyrus.Api           → Zephyrus.Application  ✅
  Zephyrus.Api           → Zephyrus.Core         ✅
  Zephyrus.Api           → Zephyrus.Infrastructure ✅  (DI wiring only)
  Zephyrus.Application   → Zephyrus.Core         ✅
  Zephyrus.Infrastructure → Zephyrus.Core        ✅

FORBIDDEN:
  Zephyrus.Core          → anything              ❌
  Zephyrus.Application   → Zephyrus.Infrastructure ❌
  Zephyrus.Application   → Zephyrus.Api          ❌
  Zephyrus.Infrastructure → Zephyrus.Application ❌
  Zephyrus.Core          → Zephyrus.Application  ❌
  Zephyrus.Core          → Zephyrus.Infrastructure ❌
```

---

## What to Check

### 1. .csproj ProjectReference violations
Scan each .csproj for `<ProjectReference>` entries.
Flag any that violate the dependency rule above.

```bash
# Run this to see all project references
grep -r "ProjectReference" src/ --include="*.csproj"
```

### 2. Namespace leakage in source files
Scan .cs files for `using` statements that cross layer boundaries.

```bash
# Check for Infrastructure types in Application
grep -r "using Zephyrus.Infrastructure" src/Zephyrus.Application/

# Check for Infrastructure or Application types in Core
grep -r "using Zephyrus.Infrastructure\|using Zephyrus.Application" src/Zephyrus.Core/

# Check for EF Core in Core or Application
grep -r "using Microsoft.EntityFrameworkCore" src/Zephyrus.Core/ src/Zephyrus.Application/

# Check for Octokit in anything other than Infrastructure
grep -r "using Octokit" src/Zephyrus.Core/ src/Zephyrus.Application/ src/Zephyrus.Api/

# Check for HttpClient in anything other than Infrastructure
grep -rn "new HttpClient\|HttpClientFactory" src/Zephyrus.Core/ src/Zephyrus.Application/
```

### 3. Business logic in Api layer
Scan controllers for logic that should be in Application.
Flag any controller action longer than 15 lines.
Flag any controller action containing if/else beyond null checks or result mapping.

### 4. Core package references
```bash
# Core must have zero external package references
grep -A5 "ItemGroup" src/Zephyrus.Core/Zephyrus.Core.csproj | grep "PackageReference"
```

---

## Output Format

```
DEPENDENCY CHECK REPORT
=======================

.CSPROJ REFERENCES
  ✅ Zephyrus.Api references Zephyrus.Application
  ✅ Zephyrus.Application references Zephyrus.Core
  ❌ Zephyrus.Application references Zephyrus.Infrastructure
     File: src/Zephyrus.Application/Zephyrus.Application.csproj
     Fix: Remove ProjectReference to Infrastructure. Use Core interfaces instead.

NAMESPACE LEAKAGE
  ✅ No Infrastructure usings in Core
  ⚠️  EF Core using found in Application
     File: src/Zephyrus.Application/UseCases/ApproveArtifactUseCase.cs:3
     using Microsoft.EntityFrameworkCore;
     Fix: Remove. Use IFeatureRepository from Core instead.

CORE PACKAGE REFERENCES
  ✅ Zephyrus.Core has zero PackageReferences

CONTROLLER COMPLEXITY
  ⚠️  FeaturesController.ApproveArtifact is 23 lines
     File: src/Zephyrus.Api/Controllers/FeaturesController.cs
     Fix: Extract approval logic to ApproveArtifactUseCase

SUMMARY
  PASS: 8 / WARN: 2 / FAIL: 1
  Action required: Yes
```

---

## After Fixing Violations

Re-run `/check-dependencies` to confirm all issues are resolved before committing.
All checks must show PASS or WARN (no FAIL) before merging to main.
