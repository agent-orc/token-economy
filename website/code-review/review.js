(() => {
  const studiesHost = document.getElementById('review-studies');
  const localHost = document.getElementById('review-local-evidence');
  if (!studiesHost || !localHost) return;
  const escape = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
  const list = value => Array.isArray(value) ? value : value ? [value] : [];
  const sourceLinks = urls => [...new Set(list(urls))].map((url, index) => `<a href="${escape(url)}">Source ${index + 1} ↗</a>`).join(' · ');
  const number = value => Number(value).toLocaleString('en-US', { maximumFractionDigits: 4 });
  window.tokenEconomyReviewReady = fetch('../data/code-review.json').then(response => {
    if (!response.ok) throw new Error('Review data unavailable');
    return response.json();
  }).then(data => {
    const types = new Map(data.benchmarkTypes.map(type => [type.id, type]));
    const records = new Map(data.records.map(record => [record.id, record]));
    const names = new Map(data.models.map(model => [model.modelId, model.displayName]));
    studiesHost.innerHTML = data.studies.map(study => {
      const rows = study.evidenceIds.map(id => records.get(id));
      const dateLabel = rows.every(row => row.context?.dateBasis === 'firstObservedPublicSnapshot') ? 'Snapshot' : 'Published';
      const tableRows = rows.map(record => {
        const type = types.get(record.benchmarkTypeId);
        const effort = record.reasoningEffort === 'unspecified' ? 'API effort unreported' : `API effort ${record.reasoningEffort}`;
        return `<tr data-review-evidence="${escape(record.id)}"><th scope="row">${escape(type.name)}<p class="source-note">${escape(type.version)}</p></th>`
          + `<td>${escape(names.get(record.modelId) || record.modelId)}<p class="source-note">${escape(effort)}</p></td>`
          + `<td><strong>${number(record.score)}</strong> ${escape(type.unit)}<p class="source-note">Original scale ${number(type.minimumScore)}–${number(type.maximumScore)}</p></td></tr>`;
      }).join('');
      const notes = rows.map(record => `<li><strong>${escape(names.get(record.modelId) || record.modelId)} · ${escape(types.get(record.benchmarkTypeId).name)}</strong><p>${escape(record.evidenceExcerpt)}</p><p class="source-note">${escape(window.tokenEconomyEvidence.context(record))}</p><a href="${escape(record.sourceUrl)}">${window.tokenEconomyEvidence.label(record)} ${escape(record.publishedAt)} ↗</a></li>`).join('');
      return `<article class="review-study" id="${escape(study.id)}"><p class="eyebrow">${dateLabel} ${escape(study.date || study.publishedAt || '')}</p><h3>${escape(study.title)}</h3><p>${escape(study.summary)}</p>`
        + (study.metricDefinition ? `<p class="source-note">${escape(list(study.metricDefinition).join(' '))}</p>` : '')
        + `<details><summary>Measurements and test setup (${rows.length})</summary><div class="table-scroll" tabindex="0" role="region" aria-label="${escape(study.title)} measurements"><table><thead><tr><th>Metric / configuration</th><th>Model / effort</th><th>Original score</th></tr></thead><tbody>${tableRows}</tbody></table></div>`
        + `<details><summary>Measurement notes and provenance</summary><ul>${notes}</ul></details></details>`
        + (list(study.limits).length ? `<p class="source-note"><strong>Scope:</strong> ${escape(list(study.limits).join(' '))}</p>` : '')
        + `<p class="source-note">${sourceLinks(study.sourceUrls || rows.map(row => row.sourceUrl))}</p></article>`;
    }).join('');
    const report = data.operational;
    localHost.innerHTML = `<div class="review-counts"><span><strong>${report.importedRunCount}</strong> imported runs</span><span><strong>${report.fixtureRunCount}</strong> excluded fixtures</span><span><strong>${report.eligibleOperationalRunCount}</strong> eligible operational runs</span></div>`
      + (report.eligibleOperationalRunCount === 0 ? '<p>The committed evidence contains no eligible operational review run. Local finding precision, recall and per-model review suitability are therefore unavailable. Published model measurements above remain available independently.</p>' : '<p>Operational observations are available. Read the cohort coverage and gate failures before using their compatibility signal.</p>')
      + `<p><a href="https://github.com/agent-orc/token-economy/blob/main/${escape(data.source.operationalEvidence)}">Committed operational report</a> · <a href="../api/#review-runs">Read coverage and qualification in C#</a></p>`;
    const revealStudy = () => {
      let id;
      try { id = decodeURIComponent(location.hash.slice(1)); } catch { return; }
      const target = document.getElementById(id);
      if (target?.classList.contains('review-study')) {
        target.querySelector('details').open = true;
        target.scrollIntoView({ block: 'start' });
      }
    };
    window.addEventListener('hashchange', revealStudy);
    document.addEventListener('click', event => {
      const link = event.target.closest('a[href^="#"]');
      if (link && link.hash === location.hash) revealStudy();
    });
    revealStudy();
  }).catch(() => {
    studiesHost.innerHTML = '<p>Review measurements could not be loaded. <a href="../data/code-review.json">Open the review data</a> or <a href="../api/#review-benchmarks">read the API contract</a>.</p>';
    localHost.innerHTML = '<p>Local coverage could not be loaded. <a href="../api/#review-runs">Read the review evidence contract</a>.</p>';
  }).finally(() => {
    for (const host of [studiesHost, localHost]) {
      host.removeAttribute('data-pending-content');
      host.removeAttribute('aria-live');
    }
    window.dispatchEvent(new CustomEvent('site:content-ready'));
  });
})();
