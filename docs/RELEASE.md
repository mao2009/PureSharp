# PureSharp release procedure

This is the reproducible release checklist for the PureSharp v1 line. `Directory.Build.props` is the version SSOT.

## Release invariants

A release is blocked unless all of the following are true:

- The release commit is on `main` and the working tree is clean.
- `Directory.Build.props` contains exactly one repository `<Version>` value.
- The packed NuGet version equals the SSOT version.
- The Git tag is exactly `v<Version>` and points at the release commit.
- Restore, Release build, tests, pack, clean-consumer verification, and analyzer performance verification all pass.
- `docs/DIAGNOSTICS.md` is frozen for the release and matches implementation/tests.
- Documentation, migration notes, compatibility policy, and changelog are current.
- No open issue classified as a v1 breaking blocker remains.
- GitHub Release and NuGet publish the same version.

## Local/reproducible verification

From a clean checkout:

```bash
bash scripts/verify-release.sh
```

To require a specific version:

```bash
bash scripts/verify-release.sh 1.0.0
```

The script performs restore/build/test/pack plus consumer and performance verification, checks package/version metadata, and fails on release-tracking format warnings.

## v0.9.x release candidate

RC versions use SemVer prerelease form, for example `0.9.0-rc.1`.

1. Start from current `main` and confirm all v1 hardening issues intended for the RC are merged.
2. Set `Directory.Build.props` to the RC version.
3. Update `CHANGELOG.md` with RC findings if needed.
4. Run `bash scripts/verify-release.sh 0.9.0-rc.1`.
5. Open a release PR and require CI green on its final HEAD.
6. Merge the PR.
7. Confirm post-merge `main` CI is green.
8. Create tag `v0.9.0-rc.1` at that exact `main` commit.
9. The tag workflow verifies the tag/version match, creates a draft GitHub Release, publishes NuGet, then publishes the GitHub Release as a prerelease.
10. Install the published prerelease from a fresh consumer and record any blocking findings.

An RC is not a requirement to change the public v1 contract. Any diagnostic-contract widening discovered after the contract freeze must be treated as a v1 blocker or deferred to the next major version.

## v1.0.0 blocking criteria

Before the v1 version PR is merged:

- Issues #7 through #16 that are classified as v1 blockers are complete.
- There is no unresolved known defect that invalidates the documented RT/LVP/FIF guarantees.
- All seven v1 diagnostic IDs, categories, default severities, and enabled states are frozen.
- `README.md`, `README_ja.md`, `docs/GETTING_STARTED.md`, `docs/MIGRATION-1.0.md`, and `CHANGELOG.md` are current.
- The package consumer gate proves analyzer discovery and `.editorconfig` suppression from the packed package.
- The representative performance result stays below the hard threshold and does not exceed 2x the recorded baseline without an accepted explanation.
- Analyzer release-tracking files build without `RS2007`.

## v1.0.0 release

1. Set the version SSOT to `1.0.0`.
2. Change the `1.0.0` changelog heading from `Unreleased` to the release date.
3. Run `bash scripts/verify-release.sh 1.0.0` from a clean checkout.
4. Open/merge the release PR after final-head CI succeeds.
5. Confirm post-merge `main` CI succeeds and record the exact main SHA.
6. Create tag `v1.0.0` at that exact SHA. Do not move/recreate the tag after publication.
7. The tag workflow must:
   - verify `v1.0.0` matches the version SSOT,
   - restore/build/test/pack,
   - verify the packed consumer and performance budget,
   - create a draft GitHub Release,
   - publish `loach.PureSharp` 1.0.0 to NuGet,
   - publish the GitHub Release only after the NuGet push succeeds.
8. Verify the GitHub Release is public and references `v1.0.0`.
9. Verify NuGet shows package version `1.0.0`.
10. Create a fresh consumer project and install exactly `loach.PureSharp` 1.0.0; run the documented Quick Start and confirm analyzer discovery.
11. Close release issue #17 and parent roadmap #6 only after the public-package verification succeeds.

## Failure handling

- If pre-tag CI fails, fix through a PR; do not tag.
- If the tag workflow fails before NuGet publication, leave/reuse the same immutable release commit and fix workflow/release infrastructure through a normal PR before deciding whether a new version is required.
- If NuGet publication succeeds but GitHub Release publication fails, do not reuse the version for different bits. Repair the GitHub Release metadata for the same tag/package.
- Never repoint a published tag to different source.

## Evidence to record

For RC and final releases record:

- version SSOT,
- release/main commit SHA,
- tag and tag target SHA,
- CI run ID/result,
- test result,
- package filename/version,
- consumer verification result,
- performance measurement,
- GitHub Release state,
- NuGet public version,
- post-release clean-consumer result.
