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

PureSharp does not publish merely because the version changes. A release PR must also add or update:

```text
release/requested-version.txt
```

The file contains only the exact version, for example:

```text
1.0.0
```

On a push to `main`, CI first runs the complete release gate and uploads the exact verified package as a workflow artifact. Only after that succeeds does the write-scoped `publish-requested-release` job compare the request marker with `Directory.Build.props`.

For a new requested version, that same successful `main` workflow then:

1. creates a draft GitHub Release and `v<Version>` tag at the exact successful `main` SHA,
2. verifies the tag target and package metadata,
3. publishes the already-verified package to NuGet,
4. creates a brand-new consumer that restores the exact version from **nuget.org only** and proves analyzer discovery plus `.editorconfig` suppression,
5. publishes the GitHub Release only after the public-package verification succeeds.

Publication intentionally stays in the same `main` workflow run. GitHub does not create a new workflow run for ordinary events produced with the repository `GITHUB_TOKEN`, so correctness must not depend on a workflow-created tag push triggering another run. A separately created external/manual `v*` tag is still supported by the `publish-tag` job.

If the requested release is already public, subsequent `main` pushes are a no-op for that request marker.

## v0.9.x release candidate

RC versions use SemVer prerelease form, for example `0.9.0-rc.1`.

1. Start from current `main` and confirm all v1 hardening issues intended for the RC are merged.
2. Set `Directory.Build.props` to the RC version.
3. Put the same version in `release/requested-version.txt`.
4. Update `CHANGELOG.md` with RC findings if needed.
5. Run `bash scripts/verify-release.sh 0.9.0-rc.1`.
6. Open a release PR and require CI green on its final HEAD.
7. Merge the PR.
8. The post-merge `main` release gate must pass. The same workflow creates the RC tag/draft release, publishes NuGet, verifies the public package, and then publishes the GitHub prerelease.
9. Record the public-package verification result and any blocking findings.

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
4. Pin the canonical Quick Start/install commands to `loach.PureSharp` 1.0.0.
5. Run `bash scripts/verify-release.sh 1.0.0` from a clean checkout.
6. Open/merge the release PR after final-head CI succeeds.
7. Confirm the post-merge `main` release gate succeeds and record the exact main SHA.
8. The same `main` run must create `v1.0.0` at that SHA, create a draft GitHub Release, publish NuGet, verify a fresh nuget.org-only consumer, and then publish the GitHub Release.
9. Verify the public GitHub Release references `v1.0.0` and the release SHA.
10. Verify NuGet shows package version `1.0.0`; the workflow's public-consumer step is the executable post-release install proof.
11. Close release issue #17 and parent roadmap #6 only after the public artifacts and post-release consumer verification succeed.

## Failure handling

- If release-PR or post-merge main CI fails, fix through a PR; no release is published.
- If the release request and SSOT differ, publication fails before a tag/release is created.
- If a draft release/tag is created but publication subsequently fails, keep that tag immutable. Re-run the same failed workflow when possible; do not point the same tag/version at different bits.
- If NuGet publication succeeds but public consumer verification or GitHub Release publication fails, do not reuse the version for different bits. Repair publication metadata/infrastructure for the same tag/package.
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
- packed consumer verification result,
- performance measurement,
- NuGet public version,
- post-release nuget.org-only consumer result,
- GitHub Release state.
