# TE-4 cap-forecast rendered evidence

These screenshots are full-page renders of the actual
[`website/cap-forecast/index.html`](../website/cap-forecast/index.html) concept
overview. They are task-specific evidence; the older `website-*--real.png`
files in this folder belong to the TE-3 website delivery.

| View | Rendered evidence | Full-page image size |
| --- | --- | --- |
| Desktop viewport (1440 × 900 CSS px) | [`cap-forecast-overview-desktop--real.png`](cap-forecast-overview-desktop--real.png) | 1440 × 2544 px |
| Mobile viewport (390 × 844 CSS px) | [`cap-forecast-overview-mobile--real.png`](cap-forecast-overview-mobile--real.png) | 390 × 4349 px |

Both images were captured from the local HTML with Chromium device metrics and
no content substitutions. The example values visible in the page
remain explicitly illustrative, not measured provider data. Visual inspection
confirms that the desktop card grids render in three columns; at 390 px the
cards stack, the page copy remains inside the body viewport, and the
intentionally wide comparison table remains inside its own horizontal scroll
region.

Regenerate both artifacts from the repository root with:

```text
node scripts/capture-cap-forecast-evidence.mjs
```

The capture script requires Node.js 22 or later, waits for page load and fonts,
measures the rendered content, and saves full-page PNGs under `results/`. Set
`CHROME_PATH` if Chrome or Edge is not installed in a standard location.
