#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd -- "$repo_root"

configuration="${DOTNET_CONFIGURATION:-Release}"
startup_project="src/Apps/InkFlow.Api/InkFlow.Api.csproj"

checks=(
  "Identity|src/Modules/InkFlow.Modules.Identity/InkFlow.Modules.Identity.csproj|InkFlow.Modules.Identity.Infrastructure.Persistence.IdentityDbContext"
  "Audit|src/BuildingBlocks/InkFlow.BuildingBlocks.Persistence/InkFlow.BuildingBlocks.Persistence.csproj|InkFlow.BuildingBlocks.Persistence.AuditDbContext"
  "Messaging|src/BuildingBlocks/InkFlow.BuildingBlocks.Persistence/InkFlow.BuildingBlocks.Persistence.csproj|InkFlow.BuildingBlocks.Persistence.MessagingDbContext"
  "Developers|src/Modules/InkFlow.Modules.Developers/InkFlow.Modules.Developers.csproj|InkFlow.Modules.Developers.Infrastructure.Persistence.DeveloperDbContext"
  "Billing|src/Modules/InkFlow.Modules.Billing/InkFlow.Modules.Billing.csproj|InkFlow.Modules.Billing.Infrastructure.Persistence.BillingDbContext"
  "Operations|src/Modules/InkFlow.Modules.Operations/InkFlow.Modules.Operations.csproj|InkFlow.Modules.Operations.Infrastructure.Persistence.OperationsDbContext"
  "Crawling|src/Modules/InkFlow.Modules.Crawling/InkFlow.Modules.Crawling.csproj|InkFlow.Modules.Crawling.Infrastructure.Persistence.CrawlingDbContext"
  "Library|src/Modules/InkFlow.Modules.Library/InkFlow.Modules.Library.csproj|InkFlow.Modules.Library.Infrastructure.Persistence.LibraryDbContext"
  "Reading|src/Modules/InkFlow.Modules.Reading/InkFlow.Modules.Reading.csproj|InkFlow.Modules.Reading.Infrastructure.Persistence.ReadingDbContext"
  "Sources|src/Modules/InkFlow.Modules.Sources/InkFlow.Modules.Sources.csproj|InkFlow.Modules.Sources.Infrastructure.Persistence.SourcesDbContext"
  "Content|src/Modules/InkFlow.Modules.Content/InkFlow.Modules.Content.csproj|InkFlow.Modules.Content.Infrastructure.Persistence.ContentDbContext"
)

for check in "${checks[@]}"; do
  IFS='|' read -r name project context <<< "$check"
  printf 'Checking %s migration model...\n' "$name"
  dotnet tool run dotnet-ef migrations has-pending-model-changes \
    --project "$project" \
    --startup-project "$startup_project" \
    --context "$context" \
    --configuration "$configuration" \
    --no-build
done

printf 'verify-migrations: PASS (%s contexts, configuration=%s)\n' "${#checks[@]}" "$configuration"
