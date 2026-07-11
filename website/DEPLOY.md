# Deploying the Token Economy website

The site in [`website/`](.) is **plain static HTML with no build step** (the
runner-website pattern). It is served by Caddy on the shared
`agent-orchestrator.dev` family VM (Hetzner) from `/srv/sites/token-economy/`,
under the public path **<https://agent-orchestrator.dev/token-economy/>**.

Two things deploy it:

1. **[`.github/workflows/deploy-website.yml`](../.github/workflows/deploy-website.yml)**
   — this repo's CI. On every push to `main` that touches `website/**` (or the
   workflow itself), it `rsync`s `website/` over SSH into `/srv/sites/token-economy/`
   on the VM. It can also be run manually from the Actions tab
   (`workflow_dispatch`).
2. **The hosting side** — a Caddy route and a `sites.json` entry that live in the
   **private** hosting meta-repo `agent-orchestrator-website`. This is a
   **one-time** operator step (below). The VM config and credentials live only in
   that private repo; nothing here contains or guesses them.

---

## One-time operator step (private hosting meta-repo)

> These edits belong in the **private** `agent-orchestrator-website` meta-repo,
> not in this repository. The exact `sites.json` schema and Caddyfile layout are
> whatever that repo already uses for the other family sites — mirror an existing
> entry (e.g. the runner website) and adapt the two values below. Do **not** guess
> credentials or the VM's real paths; use the ones the meta-repo already defines.

### 1. Create the served directory on the VM

```bash
sudo mkdir -p /srv/sites/token-economy
sudo chown "$DEPLOY_USER":"$DEPLOY_USER" /srv/sites/token-economy   # the CI deploy user
```

The first CI deploy fills it; you do not need to copy files by hand.

### 2. Add the Caddy route

Add a subpath handler to the `agent-orchestrator.dev` site block (adapt to the
meta-repo's actual Caddyfile structure — it may use snippets or imports):

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
relative asset files), so it is robust under the subpath either way. Reload
Caddy the way the meta-repo does (typically `sudo systemctl reload caddy` or
`caddy reload`). A **content** redeploy needs no reload — only this initial route
addition does.

### 3. Register it in `sites.json`

Add an entry alongside the other family sites. Example shape — **match the
meta-repo's real schema**:

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

---

## GitHub secrets the deploy needs

Set these on this repo (Settings → Secrets and variables → Actions), scoped to
the `production` environment the workflow binds to. The values come from the
private hosting meta-repo / VM owner — **do not invent them**:

| Secret | What it is |
| --- | --- |
| `DEPLOY_HOST` | VM hostname or IP for the `agent-orchestrator.dev` box. |
| `DEPLOY_USER` | SSH user the deploy writes as (owns `/srv/sites/token-economy`). |
| `DEPLOY_SSH_KEY` | Private SSH key (PEM) whose public half is in that user's `authorized_keys`. Use a **dedicated deploy key**, not a personal key. |
| `DEPLOY_KNOWN_HOSTS` | The VM's SSH host public key line, for host-key pinning. Generate with `ssh-keyscan -t ed25519 <host>` and verify the fingerprint out-of-band. |

The workflow fails fast with a clear message if any secret is missing.

---

## Preview locally

No build, no dependencies — open the file or serve the folder:

```bash
# simplest: open website/index.html in a browser
# or serve the subpath exactly as production does:
python3 -m http.server 8080 --directory website
# → http://localhost:8080/
```

## What deploys

`rsync -az --delete` mirrors the **contents** of `website/` into
`/srv/sites/token-economy/`, so the live site is always an exact copy of this
folder on `main` — files removed here are removed on the VM. Keep the folder
self-contained (no external requests) so it works offline and under the subpath.
