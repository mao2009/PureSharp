# Contributing to PureSharp

PureSharp treats analyzer behavior as a public contract. Changes should be small, reviewable, and backed by evidence.

## Before changing an analyzer

1. Identify the diagnostic ID and read [`docs/DIAGNOSTICS.md`](docs/DIAGNOSTICS.md).
2. Read the specialized contract for the affected family.
3. Add or update positive, negative, and boundary tests before changing behavior.
4. Decide whether the change widens or narrows detection.
5. Apply the compatibility policy before choosing a release line.

## Diagnostic compatibility

For v1 and later:

- Do not rename an existing diagnostic ID.
- Category changes, higher default severity, wider detection, and diagnostic removal require a major release.
- Narrower detection and lower severity may be minor changes when documented.
- Message and translation wording are not compatibility surface.
- New default-error diagnostics are breaking because they can fail existing consumer builds.

The complete policy is normative in [`docs/DIAGNOSTICS.md`](docs/DIAGNOSTICS.md).

## Required verification

For analyzer or packaging changes, run the same core sequence as CI:

```bash
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet pack src/PureSharp.Core/PureSharp.Core.csproj --configuration Release -o out --no-build
bash scripts/verify-consumer-package.sh out
bash scripts/verify-analyzer-performance.sh out
```

Documentation-only changes should still keep CI green. Release changes must also follow [`docs/RELEASE.md`](docs/RELEASE.md) once present.

## Pull-request scope

Prefer one issue per PR. State the base commit, resulting commit, verification evidence, and whether the change affects the public diagnostic contract. Do not report unverified build/test/CI state as successful.

## Documentation

Follow [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md). Behavioral guarantees belong in the canonical contract documents, not only in translated READMEs.

## Adding a diagnostic

A new diagnostic requires, in the same change:

- a unique ID following the family convention,
- descriptor/resource entries,
- `docs/DIAGNOSTICS.md` and README updates,
- positive and negative analyzer tests,
- an example,
- release-tracking metadata,
- a compatibility/release decision.
