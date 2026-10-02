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

## Explicit release request

PureSharp does not create a release tag merely because the version changes. A release PR must also add or update:

```text
release/requested-version.txt
```

The file contains only the exact version, for example:

```text
1.0.0
```

On a push to `main`, CI first runs the complete release gate. Only after that job succeeds does the write-scoped `request-release-tag` job compare the request marker with `Directory.Build.props`. If they match and `v<Version>` does not already exist, CI creates the tag at that exact successful `main` SHA. The tag push then starts the publication run.

This makes tag creation an explicit, reviewable action while preventing a tag from being created before the main release gate succeeds.

## v0.9.x release candidate

RC versions use SemVer prerelease form, for example `0.9.0-rc.1`.

1. Start from current `main` and confirm all v1 hardening issues intended for the RC are merged.
2. Set `Directory.Build.props` to the RC version.
3. Put the same version in `release/requested-version.txt`.
4. Update `CHANGELOG.md` with RC findings if needed.
5. Run `bash scripts/verify-release.sh 0.9.0-rc.1`.
6. Open a release PR and require CI green on its final HEAD.
7. Merge the PR.
8. The post-merge `main` release gate must pass. CI then creates `v0.9.0-rc.1` at that exact commit.
9. The tag workflow verifies the tag/version/package match, creates a draft GitHub Release, publishes NuGet, then publishes the GitHub Release as a prerelease.
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
2. Set `release/requested-version.txt` to `1.0.0`.
3. Change the `1.0.0` changelog heading from `Unreleased` to the release date.
4. Run `bash scripts/verify-release.sh 1.0.0` from a clean checkout.
5. Open/merge the release PR after final-head CI succeeds.
6. Confirm the post-merge `main` release gate succeeds and record the exact main SHA.
7. After that gate succeeds, the release-request job creates `v1.0.0` at the same SHA. Do not move/recreate the tag after publication.
8. The tag workflow must:
   - verify `v1.0.0` matches the version SSOT,
   - rerun the complete release gate,
   - verify the packed package metadata,
   - create a draft GitHub Release,
   - publish `loach.PureSharp` 1.0.0 to NuGet,
   - publish the GitHub Release only after the NuGet push succeeds.
9. Verify the GitHub Release is public and references `v1.0.0`.
10. Verify NuGet shows package version `1.0.0`.
11. Create a fresh consumer project and install exactly `loach.PureSharp` 1.0.0; run the documented Quick Start and confirm analyzer discovery.
12. Close release issue #17 and parent roadmap #6 only after the public-package verification succeeds.

## Failure handling

- If release-PR or post-merge main CI fails, fix through a PR; no release tag is created.
- If the release request and SSOT differ, tagging fails before publication.
- If the tag workflow fails before NuGet publication, keep the immutable release commit and fix release infrastructure through a normal PR before deciding whether a new version is required.
- If NuGet publication succeeds but GitHub Release publication fails, do not reuse the version for different bits. Repair the GitHub Release metadata for the same tag/package.
- Never repoint a published tag to different source.

## Evidence to record

For RC and final releases record:

- version SSOT,
- release request version,
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
