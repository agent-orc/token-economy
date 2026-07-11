# Operator step — put the Token Economy site online (hosting side)

This is the **one-time, manual operator step** for TE-3. Everything in *this*
repository (the static site in `website/` and the CI workflow
`.github/workflows/deploy-website.yml`) is done and needs no further action. What
remains is the **hosting side**, which lives in the **private** meta-repo
`agent-orchestrator-website` and on the Hetzner VM — it cannot live here, and no
credentials, hostnames, or real paths are guessed in this repo.

> The canonical, in-repo version of these instructions is
> [`website/DEPLOY.md`](../website/DEPLOY.md) (it ships next to the site). This
> file is the short operator hand-off for the job's `results/` folder; follow
> either — they agree.

Public URL once live: **<https://agent-orchestrator.dev/token-economy/>**
Served subdir on the VM: **`/srv/sites/token-economy/`** (the Caddy `/srv/sites`
family pattern; one subdirectory per family site).

---

## Checklist

### 1. Set the four GitHub Actions secrets (this repo)

`Settings → Secrets and variables → Actions`, scoped to the **`production`**
environment the workflow binds to. Values come from the VM owner / private
meta-repo — **do not invent them**:

| Secret | What it is |
| --- | --- |
| `DEPLOY_HOST` | VM hostname or IP for the `agent-orchestrator.dev` box. |
| `DEPLOY_USER` | SSH user the deploy writes as (owns `/srv/sites/token-economy`). |
| `DEPLOY_SSH_KEY` | Private SSH key (PEM), a **dedicated deploy key** — not a personal key. |
| `DEPLOY_KNOWN_HOSTS` | The VM's SSH host key line for pinning: `ssh-keyscan -t ed25519 <host>` (verify the fingerprint out-of-band). |

The workflow fails fast with a clear `::error::` if any of these is missing.

### 2. Create the served directory on the VM (once)

```bash
sudo mkdir -p /srv/sites/token-economy
sudo chown "$DEPLOY_USER":"$DEPLOY_USER" /srv/sites/token-economy   # the CI deploy user
```

The first CI deploy fills it — no manual file copy needed.

### 3. Add the Caddy route (private meta-repo `agent-orchestrator-website`)

Mirror an existing family entry (e.g. the runner website) and adapt. Add a
subpath handler to the `agent-orchestrator.dev` site block:

```caddy
agent-orchestrator.dev {
    # … existing family routes …

    handle_path /token-economy/* {
        root * /srv/sites/token-economy
        file_server
    }
}
```

`handle_path` strips the `/token-economy` prefix, so `/token-economy/` serves
`/srv/sites/token-economy/index.html`. The page inlines its CSS and favicon (no
relative asset files), so it is robust under the subpath. Reload Caddy the way
the meta-repo does (typically `sudo systemctl reload caddy`). Only this initial
route addition needs a reload; later **content** redeploys do not.

### 4. Register it in `sites.json` (private meta-repo)

Add an entry alongside the other family sites — **match that repo's real
schema**; this is only the shape:

```jsonc
{
  "slug": "token-economy",
  "title": "Token Economy",
  "path": "/token-economy/",
  "root": "/srv/sites/token-economy",
  "repo": "agent-orc/token-economy",
  "source": "website/"
}
```

### 5. Trigger the first deploy

Push to `main` touching `website/**` (this task's changes do), or run
**`deploy-website`** manually from the repo's **Actions** tab
(`workflow_dispatch`). It `rsync -az --delete`s the contents of `website/` into
`/srv/sites/token-economy/` (excluding the repo-internal `README.md` /
`DEPLOY.md`). Confirm <https://agent-orchestrator.dev/token-economy/> loads.

---

## Boundary (why the split)

- **In this repo:** the site (`website/`), the deploy workflow, and these docs.
- **Not in this repo (private meta-repo + VM):** the Caddy route, the
  `sites.json` entry, and the four secret values. The VM config and credentials
  live only there; nothing here contains or guesses them — per the task's
  "do not guess credentials" constraint.
