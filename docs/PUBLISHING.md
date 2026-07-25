# Publishing `TokenEconomy` to nuget.org

This package publishes through GitHub Actions
([`.github/workflows/release.yml`](../.github/workflows/release.yml)) using
nuget.org **Trusted Publishing** (OIDC): there is **no API key** stored in the
repository. Pushing a version tag builds, tests, packs, and pushes the package,
then creates a GitHub Release for the tag.

```bash
scripts/release.sh <version>
```

The tag push triggers `release.yml`, which:

1. derives the package version from the tag,
2. restores, tests, and packs `src/TokenEconomy/TokenEconomy.csproj`,
3. obtains a short-lived key via `NuGet/login@v1`,
4. pushes the `.nupkg` and its symbols to nuget.org, and
5. creates a GitHub Release carrying the package artifacts.

## First publish of a new package id: one-time setup

Before the first release, the nuget.org owner must create a Trusted Publishing
policy. If `TokenEconomy` is still unclaimed and the first OIDC publish is not
accepted, claim it once through nuget.org's manual upload page, then use the
keyless workflow for all later releases.

These steps follow Microsoft's
[Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing)
and
[package publishing](https://learn.microsoft.com/nuget/nuget-org/publish-a-package)
guides. The policy values below come directly from `release.yml`.

| Policy field | Value |
| --- | --- |
| nuget.org policy owner | individual account `RobertMischke2` |
| Repository Owner | `agent-orc` |
| Repository | `token-economy` |
| Workflow File | `release.yml` |
| Environment | leave empty |

The nuget.org account `RobertMischke2` and GitHub owner `agent-orc` are
different accounts on different systems; enter each as shown. The official
Trusted Publishing policy is owner-scoped and does not ask for a package-id
glob. Enter only `release.yml`, not `.github/workflows/release.yml`. Leave
Environment empty because the workflow does not declare a GitHub environment.

### Preferred path: configure Trusted Publishing first

1. Sign in to nuget.org as **`RobertMischke2`**.
2. Open the account menu, choose **Trusted Publishing**, and add a GitHub
   Actions policy owned by the individual account.
3. Enter the four GitHub fields from the table and save the policy.
4. From a clean `main`, run `scripts/release.sh <version>`. The tag-triggered
   workflow obtains a short-lived NuGet key through OIDC and publishes.
5. Confirm the package version appears at
   `https://www.nuget.org/packages/TokenEconomy/<version>`. New packages can
   take several minutes to validate and index.

For some repositories a new policy is shown as temporarily active for seven
days until the first successful publish supplies immutable GitHub owner and
repository IDs. Publish within that window; if it expires, restart the window
in nuget.org and rerun the failed workflow.

### Fallback: manually claim an unowned id once

Use this only if the policy was created correctly but nuget.org rejects the
first OIDC publish because the new `TokenEconomy` id has no owner.

1. From clean `main`, run `scripts/pack.sh <version>`.
2. Sign in to nuget.org as `RobertMischke2`, select **Upload**, and choose
   `artifacts/TokenEconomy.<version>.nupkg`.
3. On the Verify page, confirm package id `TokenEconomy`, the intended version,
   README, license, and repository link, then select **Submit**. This makes the
   signed-in account the package owner without creating an API key.
4. Confirm the version at
   `https://www.nuget.org/packages/TokenEconomy/<version>`.
5. Create or re-enable the Trusted Publishing policy above, then rerun the
   release workflow for the same tag if the GitHub Release still needs to be
   created. Its package push uses `--skip-duplicate`, so the manual package
   upload is not overwritten.

## After the first publish

Every subsequent release is:

```bash
scripts/release.sh <version>
```

Trusted Publishing mints a fresh short-lived key per run; there is no stored
secret to rotate. If a release run fails authentication, re-check the policy
fields against `release.yml`. A renamed workflow, different `user:`, or added
GitHub environment requires the corresponding nuget.org policy update.
