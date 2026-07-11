# Publishing `TokenEconomy` to nuget.org

This package publishes through GitHub Actions
([`.github/workflows/release.yml`](../.github/workflows/release.yml)) using
nuget.org **Trusted Publishing** (OIDC): there is **no API key** stored in the
repo. Pushing a version tag builds, tests, packs, and pushes the package, then
creates a GitHub Release for the tag.

```
scripts/release.sh 0.1.0     # runs tests on main, tags v0.1.0, pushes the tag
```

The tag push triggers `release.yml`, which:

1. derives the version from the tag (`v0.1.0` → `0.1.0`),
2. restores, tests, and `dotnet pack`s `src/TokenEconomy/TokenEconomy.csproj`,
3. mints a short-lived key via `NuGet/login@v1` (OIDC → Trusted Publishing),
4. `dotnet nuget push`es the `.nupkg`/`.snupkg` to nuget.org, and
5. creates a GitHub Release carrying the packages as assets.

---

## ⚠️ First publish of a NEW package id — one-time setup

`TokenEconomy` has never been pushed to nuget.org, so the id is unclaimed and no
Trusted Publishing policy exists for it yet. Exactly like npm's first
`npm publish` of a new scope, the **first** publish needs a one-time manual
step; every release after that is fully automated by the workflow above.

> nuget.org's Trusted Publishing UI wording changes over time. The **values** to
> enter below are exact and come from `release.yml`; confirm the current menu
> paths against the official docs before clicking:
> <https://learn.microsoft.com/nuget/nuget-org/trusted-publishing>

The Trusted Publishing policy must match what the workflow presents. From
`release.yml` those values are:

| Policy field                | Value                                        | Source in `release.yml`        |
| --------------------------- | -------------------------------------------- | ------------------------------ |
| nuget.org account (owner)   | `RobertMischke2`                             | `NuGet/login@v1` → `user:`     |
| GitHub repository owner     | `agent-orc`                              | `RepositoryUrl` / repo slug    |
| GitHub repository name      | `token-economy`                 | repo slug                      |
| Workflow file               | `release.yml`                                | this workflow's filename       |
| Package id / glob           | `TokenEconomy`                               | `PackageId`                    |

(The nuget.org account `RobertMischke2` and the GitHub owner `agent-orc` are
deliberately different accounts on different systems — enter each as shown.)

### Option A — configure Trusted Publishing first (preferred, no key ever)

1. Sign in to nuget.org as **`RobertMischke2`**.
2. Go to **Account settings → Trusted Publishing** and **Add** a policy with the
   values in the table above.
3. If the form asks whether the policy may publish a package id that **does not
   exist yet** (create-on-first-push), **enable it** for `TokenEconomy`. Some
   nuget.org revisions only allow a policy for an *existing* id — if so, use
   Option B for the very first push, then this policy covers every later release.
4. Cut the release: `scripts/release.sh 0.1.0`. The workflow publishes with no
   key. Confirm the package appears at
   <https://www.nuget.org/packages/TokenEconomy/0.1.0>.

### Option B — manual first push to claim the id, then automate

Use this if nuget.org won't create a Trusted Publishing policy for a
not-yet-existing id. It claims the id and establishes `RobertMischke2` as owner;
after that, add the Option A policy and never touch a key again.

1. Create a **short-lived** API key at
   <https://www.nuget.org/account/apikeys> — scope: **Push new packages and
   package versions**, glob **`TokenEconomy`** (or `*`), shortest expiry offered.
   Do **not** commit it; the repo `.gitignore` already blocks `*.apikey` /
   `nuget*.key` files, but treat it as a secret regardless.
2. Pack and push locally (from a clean `main`):
   ```bash
   scripts/pack.sh 0.1.0
   dotnet nuget push "artifacts/TokenEconomy.0.1.0.nupkg" \
     --api-key "$NUGET_KEY" \
     --source https://api.nuget.org/v3/index.json
   # symbols (optional; push succeeds even if this is skipped):
   dotnet nuget push "artifacts/TokenEconomy.0.1.0.snupkg" \
     --api-key "$NUGET_KEY" \
     --source https://api.nuget.org/v3/index.json
   ```
   (Set `NUGET_KEY` in the shell only; never on the command line history if
   avoidable — e.g. `read -rs NUGET_KEY`.)
3. Verify at <https://www.nuget.org/packages/TokenEconomy>.
4. **Revoke / delete the temporary API key** on nuget.org.
5. Add the Trusted Publishing policy (Option A, steps 1–3) so future tags publish
   keylessly. Tag the *next* version via `scripts/release.sh` — the workflow now
   works end-to-end.

### After the first publish

Nothing manual is needed. Every subsequent release is just:

```
scripts/release.sh <version>
```

Trusted Publishing mints a fresh short-lived key per run; there is no stored
secret to rotate. If a release run fails auth, re-check the policy fields in the
table above against `release.yml` — a renamed workflow file or a different
`user:` will break the OIDC match.
