#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 3 || $# -gt 4 ]]; then
  echo "Usage: $0 <target-dir> <service-name> <root-namespace> [service-slug]"
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
TARGET_DIR="$1"
SERVICE_NAME="$2"
ROOT_NAMESPACE="$3"
SERVICE_SLUG="${4:-billing-entitlements-service}"
CONTRACTS_NAMESPACE="${ROOT_NAMESPACE}.Contracts"
CONTRACT_GENERATOR_NAMESPACE="${ROOT_NAMESPACE}.ContractGenerator"
SERVICE_IDENTITY="graphode-${SERVICE_SLUG}"

if [[ -e "$TARGET_DIR" ]]; then
  echo "Target already exists: $TARGET_DIR"
  exit 1
fi

mkdir -p "$TARGET_DIR"
rsync -a \
  --exclude ".git" \
  --exclude ".terraform" \
  --exclude "bin" \
  --exclude "obj" \
  "${REPO_ROOT}/" "${TARGET_DIR}/"

replace_all() {
  local from="$1"
  local to="$2"
  find "$TARGET_DIR" -type f \
    ! -path "*/.git/*" \
    ! -path "*/bin/*" \
    ! -path "*/obj/*" \
    -print0 | xargs -0 perl -0pi -e "s/\Q${from}\E/${to}/g"
}

replace_all "Graphode.BillingEntitlementsService" "$SERVICE_NAME"
replace_all "Graphode.BillingEntitlementsService" "$ROOT_NAMESPACE"
replace_all "Graphode.BillingEntitlementsService.Contracts" "$CONTRACTS_NAMESPACE"
replace_all "Graphode.BillingEntitlementsService.ContractGenerator" "$CONTRACT_GENERATOR_NAMESPACE"
replace_all "graphode-billing-entitlements-service" "$SERVICE_IDENTITY"
replace_all "billing-entitlements-service" "$SERVICE_SLUG"

rename_paths() {
  local from="$1"
  local to="$2"

  while IFS= read -r -d '' path; do
    local target="${path//$from/$to}"
    mkdir -p "$(dirname "$target")"
    mv "$path" "$target"
  done < <(find "$TARGET_DIR" -depth -name "*$from*" -print0)
}

rename_paths "Graphode.BillingEntitlementsService" "$SERVICE_NAME"
rename_paths "Graphode.BillingEntitlementsService" "$ROOT_NAMESPACE"
rename_paths "Graphode.BillingEntitlementsService.Contracts" "$CONTRACTS_NAMESPACE"
rename_paths "Graphode.BillingEntitlementsService.ContractGenerator" "$CONTRACT_GENERATOR_NAMESPACE"
rename_paths "billing-entitlements-service" "$SERVICE_SLUG"

cat <<EOF
Scaffolded service into: $TARGET_DIR

Applied replacements:
- service name: $SERVICE_NAME
- root namespace: $ROOT_NAMESPACE
- contracts namespace: $CONTRACTS_NAMESPACE
- contract generator namespace: $CONTRACT_GENERATOR_NAMESPACE
- service slug: $SERVICE_SLUG
- service identity: $SERVICE_IDENTITY

Next steps:
1. Review and replace the example ReferenceItem aggregate, DTOs and handlers.
2. Regenerate contracts:
   dotnet run --project src/${CONTRACT_GENERATOR_NAMESPACE}/${CONTRACT_GENERATOR_NAMESPACE}.csproj
3. Review helper-ssot/contracts and README before first commit.
EOF
