(() => {
  const host = document.getElementById('matrix-results');
  if (!host) return;
  const escape = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
  const words = value => String(value).replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  const source = path => `https://github.com/agent-orc/token-economy/blob/main/${path}`;
  const money = value => `$${Number(value).toFixed(2)}`;
  const status = {
    selectable: ['✓', 'Selectable', 'Allowed for the workflow roles listed below.'],
    fallbackOnly: ['↪', 'Fallback only', 'Requires an explicitly qualified fallback.'],
    unsupported: ['⊘', 'Unsupported', 'No permitted route in the current policy.'],
    restricted: ['⏸', 'Restricted', 'Blocked by the current policy.'],
    deprecated: ['−', 'Deprecated', 'Retained for history; excluded from new selections.'],
  };
  const effort = value => `<span class="effort-level">${escape(value === 'xHigh' ? 'xhigh' : value)}</span>`;
  const taskStudy = study => `<li><a href="/token-economy/task-class-routing/#${escape(study.id)}">${escape(study.label)}</a> · ${escape(study.efforts.join(' / '))}`
    + (study.outcomeRate == null ? '' : ` · <strong>${Math.round(study.outcomeRate * 100)}% passed</strong> in ${study.scenarioCount} scenarios`)
    + `<br><span class="muted">${escape(study.benchmarkStatus)}${study.primaryRecommendation ? '' : ' Listed as a comparison or fallback; the headline result belongs to another route.'}</span></li>`;
  const measured = row => row.evidence.taskStudies.filter(study => study.status !== 'policyBaseline');
  const evidenceTeaser = row => {
    const coding = row.evidence.external.filter(item => item.benchmarkId === 'artificial-analysis-coding-agent-index-v1.5-native-agents');
    const index = coding.length ? coding : row.evidence.external.filter(item => item.benchmarkId === 'artificial-analysis-intelligence-index-v4.3');
    if (index.length) return `<span class="evidence-name">${coding.length ? 'AA Coding Agent v1.5' : 'AA Intelligence v4.3'}</span>`
      + index.map(item => `<span class="evidence-metric"><strong>${item.score}</strong> pts · ${escape(item.effort)}</span>`).join('');
    const primary = measured(row).find(study => study.primaryRecommendation && study.outcomeRate != null);
    if (primary) return `<span class="evidence-name">${escape(primary.label)}</span><span class="evidence-metric"><strong>${Math.round(primary.outcomeRate * 100)}%</strong> passed · ${primary.scenarioCount} scenarios</span>`;
    if (row.evidence.external.length) return `<span class="evidence-name">Published benchmarks</span><span class="evidence-metric">${row.evidence.external.length} recorded results</span>`;
    if (measured(row).length) return '<span class="evidence-name">Local pilot comparison</span><span class="evidence-metric">Small task cohorts</span>';
    return '<span class="evidence-name">No measured score</span><span class="evidence-metric">See policy and missing evidence</span>';
  };
  const routeLabel = row => {
    const routes = row.policy.routes.map(route => `${route.effort}${route.minimumTaskScore == null ? ' · bounded decisions' : ` · task score ${route.minimumTaskScore}–${route.maximumTaskScore}`}`);
    if (row.policy.fallbacks.length) routes.push(...row.policy.fallbacks.map(route => `${route.effort} · provider fallback`));
    return routes.length ? routes.map(line => `<span>${escape(line)}</span>`).join('') : row.selectionStatus === 'selectable' ? '<span>Explicit selection</span>' : '<span>No permitted route</span>';
  };
  const details = row => {
    const [, label, explanation] = status[row.selectionStatus] || status.unsupported;
    const local = measured(row);
    const published = row.evidence.external.filter(item => item.confidence !== 'ownRun');
    const own = row.evidence.external.filter(item => item.confidence === 'ownRun');
    const benchmarkRow = item => `<tr><td>${escape(item.name)} <span class="muted">${escape(item.version)}</span><p class="muted">${escape(window.tokenEconomyEvidence.context(item))}</p>${item.evidenceExcerpt ? `<details><summary>Measurement notes</summary><p>${escape(item.evidenceExcerpt)}</p></details>` : ''}</td><td>${effort(item.effort)}</td><td><strong>${window.tokenEconomyEvidence.score(item)}</strong> ${escape(item.unit)}${item.secondaryMetrics.costPerTaskUsd != null ? `<p>${money(item.secondaryMetrics.costPerTaskUsd)} / task, published</p>` : ''}</td><td><a href="${escape(item.sourceUrl)}">${window.tokenEconomyEvidence.label(item)} ${escape(item.publishedAt)} ↗</a></td></tr>`;
    const benchmarkTable = items => `<div class="table-scroll"><table class="evidence-table"><thead><tr><th>Benchmark / test setup</th><th>Effort</th><th>Measured score</th><th>Source</th></tr></thead><tbody>${items.map(benchmarkRow).join('')}</tbody></table></div>`;
    const assessment = row.evidence.assessment;
    return (assessment ? `<h4>Benchmark assessment</h4><p>${escape(assessment.summary)}</p><p><a href="model-benchmarks/#assessment-${escape(row.modelId)}">Strengths, tradeoffs and effort guidance →</a></p>` : '')
      + (published.length ? `<h4>Published benchmarks · primary comparison evidence</h4>${benchmarkTable(published)}` : '<p>No published benchmark score is linked for this model.</p>')
      + `<div class="model-detail-grid"><div><h4>${escape(label)}</h4><p>${escape(explanation)} Roles: ${escape(row.policy.workflowRoles.map(words).join(', ') || 'none')}.</p>`
      + `<p>Dated cost class: <strong>${escape(words(row.costClass))}</strong>${row.costUnconfirmed ? ' · price unconfirmed' : ''}. Reference: 1M input + 200k output tokens.</p>`
      + `<h4>${row.provisional ? 'Why provisional?' : 'Local qualification'}</h4><p>${published.length ? 'Benchmarks support the assessment above. ' : ''}${local.length ? 'The linked local task studies are pilots.' : 'A matching local task trial has not been completed.'} Qualification still requires 20 attempts, five scenarios and outcome/review coverage for the specific model, effort and task class.</p>`
      + `<p><a href="${source('docs/routing-evidence.md')}">Qualification criteria</a> · <a href="/token-economy/api/#evidence-status">Status definitions</a></p>`
      + `<details><summary>Routing policy reason and version</summary><p>${escape(row.policy.reason)}</p><p>Policy ${escape(row.policy.version)} · policy evidence ${escape(row.policy.evidenceAsOfDate)}. Newer benchmark measurements are listed above.</p></details></div>`
      + `<div><h4>Reasoning effort</h4><div class="effort-ladder">${row.effortLevels.map(effort).join('')}</div><div class="policy-effort">${routeLabel(row)}</div><p>These are supported settings. Use the measured effort beside each benchmark when comparing scores.</p><p><a href="/token-economy/api/#evaluate">Get the effort for a specific task</a></p>`
      + `<h4>Local task studies</h4>${local.length ? `<ul class="evidence-list">${local.map(taskStudy).join('')}</ul>` : '<p>No completed local pilot is linked for this model.</p>'}</div></div>`
      + (own.length ? `<details class="task-fit-details"><summary>Additional local benchmark signals (${own.length})</summary>${benchmarkTable(own)}</details>` : '')
      + `<details class="task-fit-details"><summary>AGT and policy fit · supporting signals</summary><div class="task-fit-grid">${Object.entries(row.suitability).map(([task, fit]) => `<span>${escape(words(task))}: <strong>${fit == null ? 'not assessed' : escape(fit)}</strong></span>`).join('')}</div><p>These are maintained fit estimates. The compatibility score combines task fit with dated cost weighting. AGT observations help calibrate local routing; the benchmark measurements above provide the primary model comparison.</p></details>`;
  };
  const renderRow = row => {
    const [icon, label] = status[row.selectionStatus] || status.unsupported;
    const id = `evidence-${row.modelId}`;
    const buttonAttrs = `type="button" data-evidence-toggle="${escape(id)}" aria-controls="${escape(id)}" aria-expanded="false"`;
    const price = row.price;
    return `<tr class="model-summary" data-model="${escape(row.modelId)}"><th scope="row"><strong>${escape(row.displayName)}</strong><span class="model-id">${escape(row.modelId)}</span>`
      + (row.releaseDate ? `<a class="release-date" href="${escape(row.releaseDateSource || source('src/TokenEconomy/catalog/model-prices.json'))}">Released ${escape(row.releaseDate)}</a>` : '<span class="release-date">Release date unverified</span>')
      + `</th><td class="routing-icons"><button ${buttonAttrs} class="status-icon status-${escape(row.selectionStatus)}" title="${escape(label)} — show why" aria-label="${escape(label)}: show policy and evidence for ${escape(row.displayName)}">${icon}</button>`
      + (row.provisional ? `<button ${buttonAttrs} class="status-icon status-provisional" title="Provisional evidence — show what is missing" aria-label="Provisional evidence: show what is missing for ${escape(row.displayName)}">△</button>` : '')
      + `</td><td><div class="effort-ladder">${row.effortLevels.map(effort).join('')}</div><div class="policy-effort">${routeLabel(row)}</div></td>`
      + `<td>${price.status === 'resolved' ? `<span class="matrix-price"><span>In</span> ${money(price.inputPerMTok)}</span><span class="matrix-price"><span>Out</span> ${money(price.outputPerMTok)}</span><span class="matrix-price"><span>Cache</span> ${money(price.cachedInputPerMTok)}${price.cachedInputUsesInputFallback ? '*' : ''}</span><a class="release-date" href="price-history/#price-${escape(row.modelId)}">History &amp; sources</a>` : '<span class="muted">Unpriced</span>'}</td>`
      + `<td><button ${buttonAttrs} class="evidence-button" aria-label="Show scores and evidence for ${escape(row.displayName)}">${evidenceTeaser(row)}<span class="evidence-link">Scores &amp; evidence <span aria-hidden="true">＋</span></span></button></td></tr>`
      + `<tr class="model-detail-row" id="${escape(id)}" hidden><td colspan="5"><div class="model-detail-content">${details(row)}</div></td></tr>`;
  };
  const renderTable = rows => `<div class="table-scroll matrix-scroll" tabindex="0" role="region" aria-label="Models, reasoning effort, prices and evidence"><table class="matrix-table"><thead><tr><th>Model · newest first</th><th><abbr title="Selection permission and evidence maturity">Status</abbr></th><th>Reasoning <span class="table-hint">supported levels / policy route</span></th><th>USD / MTok</th><th>Measured evidence</th></tr></thead><tbody>${rows.map(renderRow).join('')}</tbody></table></div>`;
  window.tokenEconomyMatrixReady = fetch('data/model-efficiency-matrix.json').then(response => {
    if (!response.ok) throw new Error('Matrix request failed');
    return response.json();
  }).then(data => {
    const rows = [...data.rows].sort((a, b) => (b.releaseDate || '').localeCompare(a.releaseDate || '') || a.modelId.localeCompare(b.modelId));
    const active = rows.filter(row => !row.deprecated);
    const archived = rows.filter(row => row.deprecated);
    host.innerHTML = renderTable(active)
      + (archived.length ? `<details class="model-archive"><summary>Archived models (${archived.length})</summary><p>Deprecated models remain available for historical cost lookup.</p>${renderTable(archived)}</details>` : '')
      + `<p class="data-note">Prices as of ${escape(data.asOfUtc.slice(0, 10))} UTC. Release dates determine display order; unverified dates follow dated models. * Cache uses the input rate when no separate rate is recorded.</p>`;
    host.removeAttribute('aria-live');
    const sizeDetailContent = container => {
      if (container.clientWidth > 0) {
        container.style.setProperty('--matrix-view-width', `${container.clientWidth}px`);
      }
    };
    const containers = [...host.querySelectorAll('.matrix-scroll')];
    containers.forEach(sizeDetailContent);
    if ('ResizeObserver' in window) {
      const observer = new ResizeObserver(entries => entries.forEach(entry => sizeDetailContent(entry.target)));
      containers.forEach(container => observer.observe(container));
    } else {
      window.addEventListener('resize', () => containers.forEach(sizeDetailContent));
    }
    host.addEventListener('click', event => {
      const button = event.target.closest('[data-evidence-toggle]');
      if (!button) return;
      const target = document.getElementById(button.dataset.evidenceToggle);
      const open = target.hidden;
      target.hidden = !open;
      if (open) {
        const container = target.closest('.matrix-scroll');
        sizeDetailContent(container);
        container.scrollLeft = 0;
      }
      host.querySelectorAll('[data-evidence-toggle]').forEach(control => {
        if (control.dataset.evidenceToggle !== target.id) return;
        control.setAttribute('aria-expanded', String(open));
        const sign = control.querySelector('.evidence-link > span');
        if (sign) sign.textContent = open ? '−' : '＋';
      });
    });
  }).catch(() => {
    host.innerHTML = '<p>The model matrix could not be loaded. <a href="data/model-efficiency-matrix.json">Open the data snapshot</a> or <a href="api/#describe">read the API guide</a>.</p>';
  });
})();
