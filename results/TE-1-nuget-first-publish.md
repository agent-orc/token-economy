# TE-1 NuGet first-publish handoff

Verified on 2026-07-26 against the repository release workflow, the nuget.org
V3 catalog, and Microsoft's NuGet publishing documentation.

## Current state

- Package id: `TokenEconomy`
- nuget.org owner used by the workflow: `RobertMischke2`
- Published versions returned by
  `https://api.nuget.org/v3-flatcontainer/tokeneconomy/index.json`: `0.2.0`
- Result: the package id has already had its first publish. Do **not** manually
  upload another copy of an existing version. Normal releases should use
  `scripts/release.sh <version>` from clean `main`.

Nuget.org package versions are immutable. If a published package is wrong,
unlist it if necessary, fix the repository, increment the version, and publish
the new version.

## Exact Trusted Publishing policy

The release workflow at `.github/workflows/release.yml` already requests
`id-token: write`, runs `NuGet/login@v1`, and passes the returned short-lived
key to `dotnet nuget push`. No repository API-key secret is required.

If the policy ever needs to be created or recreated:

1. Sign in to nuget.org as `RobertMischke2`.
2. Open the account menu, select **Trusted Publishing**, and add a GitHub
   Actions policy owned by the individual account.
3. Enter these values:

   | Policy field | Value |
   | --- | --- |
   | Repository Owner | `agent-orc` |
   | Repository | `token-economy` |
   | Workflow File | `release.yml` |
   | Environment | leave empty |

4. Save the policy.
5. From clean `main`, run `scripts/release.sh <new-version>`.
6. Confirm the version at
   `https://www.nuget.org/packages/TokenEconomy/<new-version>` and under
   **Manage packages**. Nuget.org validation and search indexing are
   asynchronous.

The workflow field is the filename only, not
`.github/workflows/release.yml`. Leave Environment empty because this
workflow does not declare a GitHub Actions environment.

A new policy can appear temporarily active for seven days while nuget.org
waits for the first successful publish to bind immutable GitHub owner and
repository ids. Publish during that window. If it expires, restart the window
in nuget.org before rerunning the workflow.

## One-time manual upload fallback for a new package id

This section is retained for a future **new, unowned package id** or if the
operator must establish ownership before Trusted Publishing is accepted. It
does not apply to an already-published `TokenEconomy` version.

1. From clean `main`, build the intended version:

   ```bash
   scripts/pack.sh <version>
   ```

2. Sign in to nuget.org as `RobertMischke2`, select **Upload**, and choose
   `artifacts/TokenEconomy.<version>.nupkg`.
3. On **Verify**, confirm the package id, version, README rendering, license,
   and repository link. If metadata is wrong, stop, fix the project, repack,
   and upload the replacement artifact.
4. Select **Submit**. The signed-in account becomes the package owner.
5. Confirm the version under **Manage packages** and on its package page.
6. Create or re-enable the Trusted Publishing policy above for subsequent
   releases.
7. If the tag workflow still needs to create the GitHub Release, rerun it for
   that tag. Its package push uses `--skip-duplicate`, so the existing version
   is not replaced.

Do not create a long-lived API key merely for this fallback; the nuget.org web
upload establishes ownership directly.

## Sources

- Microsoft, **Trusted Publishing on nuget.org**:
  <https://learn.microsoft.com/nuget/nuget-org/trusted-publishing>
- Microsoft, **Publish NuGet packages**:
  <https://learn.microsoft.com/nuget/nuget-org/publish-a-package>
- Repository operating guide: [`docs/PUBLISHING.md`](../docs/PUBLISHING.md)
- Executable release contract:
  [`.github/workflows/release.yml`](../.github/workflows/release.yml)

## Cross-repository compatibility follow-up

TE-1 also requires the retained `CodingAgentRunner.Pricing` surface to be
marked obsolete in coding-agent-runner 0.6.0, with a forwarding note to
`TokenEconomy`, while remaining binary/source compatible for 0.5.0 consumers.
That change belongs in a separate coding-agent-runner worktree and commit. The
TE-1 Token Economy worktree is intentionally not allowed to edit or commit the
runner repository.
