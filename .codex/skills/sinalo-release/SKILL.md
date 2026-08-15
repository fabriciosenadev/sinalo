---
name: sinalo-release
description: Prepare, validate and publish a versioned Sinalo Windows release. Use when asked to create an installer, increment the Sinalo version, publish a GitHub release, diagnose a Sinalo release failure, or verify the release pipeline.
---

# Release do Sinalo

Use this skill only in the Sinalo repository. Confirm that `Sinalo.slnx`, `releaser.ps1`, `installer/Sinalo.iss` and `.github/workflows/release.yml` exist before proceeding.

## Prepare a release

1. Inspect `git status --short` and the current project version in `src/Sinalo.App/Sinalo.App.csproj`.
2. Stop if unrelated local changes exist; preserve them unless the user explicitly includes them in the release.
3. Run `./releaser.ps1 -NextPatch` from the repository root. It runs tests, enforces 75% line and branch coverage, publishes `win-x64`, generates the Inno Setup installer, and updates the project version only after success.
4. Verify the installer path printed by the script and the changed version file.

## Publish through the current GitHub workflow

1. Do not commit, push, tag or publish without explicit user approval.
2. After approval, commit only the release-version changes and push `main`.
3. Create an annotated tag matching the project version exactly, in the form `vX.Y.Z`, then push that tag.
4. The `release.yml` workflow builds the installer, creates/updates the GitHub Release, and attaches the installer artifact.
5. Check the Actions run and confirm the release asset exists before reporting success.

## Failure rules

- If tests or coverage fail, fix the underlying code or tests; never reduce the 75% thresholds.
- If the SDK is missing, align `global.json` only with an installed SDK after verifying it with `dotnet --list-sdks`.
- If Inno Setup is unavailable, report the missing prerequisite and preserve the generated publish output.
- If the GitHub workflow fails, inspect its logs before modifying workflow files.

## Safety

- Keep generated installers, `.release` output, `bin`, `obj`, and `TestResults` out of commits.
- Do not replace a published tag or release asset without explicit user approval.
