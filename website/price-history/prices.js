(() => {
  const tableHost = document.getElementById('price-table-host');
  const historyHost = document.getElementById('price-history-host');
  const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const usd = value => value == null ? '—' : '$' + Number(value).toLocaleString('en-US', { maximumFractionDigits: 5 });
  const date = value => String(value || '').slice(0, 10);
  const rateAt = (row, at) => [...row.history].reverse().find(p => p.validFrom <= at && (!p.validTo || p.validTo >= at));
  const amount = price => price ? price.inputPerMTok * .1 + price.outputPerMTok * .02 : null;
  const states = { selectable:'Selectable', restricted:'Restricted access', unsupported:'Unsupported for routing', deprecated:'Archived by routing policy' };
  const table = (rows, at, referenceAmount) => `<div class="table-scroll" tabindex="0" role="region" aria-label="Model prices and relative consumption"><table class="price-table"><thead><tr><th scope="col">Model · newest first</th><th scope="col">Input</th><th scope="col">Output</th><th scope="col">Cache read / write</th><th scope="col">Example amount</th><th scope="col">Index</th></tr></thead><tbody>${rows.map(row => {
    const p = rateAt(row, at);
    const total = amount(p);
    return `<tr data-price-model="${esc(row.modelId)}"><th scope="row"><a href="#price-${esc(row.modelId)}">${esc(row.displayName)}</a><small class="model-id">${esc(row.modelId)}</small><small>${esc(row.releaseDate)} · ${esc(states[row.selectionStatus] || row.selectionStatus)}</small></th>`
      + (p ? `<td>${usd(p.inputPerMTok)}</td><td>${usd(p.outputPerMTok)}</td><td>${usd(p.cacheReadPerMTok ?? p.inputPerMTok)} / ${usd(p.cacheWritePerMTok ?? p.inputPerMTok)}${p.cacheWritePerMTok == null ? '<small>Write: input-rate fallback</small>' : ''}</td><td>${usd(total)}</td><td class="price-index">${referenceAmount > 0 ? (total / referenceAmount).toFixed(2) + '×' : '—'}</td>` : '<td colspan="5">No price for this date</td>') + '</tr>';
  }).join('')}</tbody></table></div>`;
  const sourceLinks = p => (p.sourceUrls || []).map((url, i) => `<a href="${esc(url)}">${esc(new URL(url).hostname.replace(/^www\./, ''))}${p.sourceUrls.filter(u => new URL(u).hostname === new URL(url).hostname).length > 1 ? ` [${i + 1}]` : ''}</a>`).join(' · ');
  const history = row => `<details class="price-history" id="price-${esc(row.modelId)}"><summary>${esc(row.displayName)} <small>${row.history.length} price ${row.history.length === 1 ? 'period' : 'periods'} · released ${esc(row.releaseDate)}</small></summary>`
    + `<p class="history-meta"><code>${esc(row.modelId)}</code> · ${esc(states[row.selectionStatus] || row.selectionStatus)}${row.note ? '<br>' + esc(row.note) : ''}</p>`
    + `<div class="table-scroll" tabindex="0" role="region" aria-label="${esc(row.displayName)} price history"><table class="price-table"><thead><tr><th scope="col">Effective period (UTC)</th><th scope="col">Input / output</th><th scope="col">Cache read / write</th><th scope="col">Evidence</th></tr></thead><tbody>${[...row.history].reverse().map(p => `<tr><th scope="row">${esc(date(p.validFrom))}<small>through ${p.validTo ? esc(date(p.validTo)) : 'no recorded end'}</small></th><td>${usd(p.inputPerMTok)} / ${usd(p.outputPerMTok)}</td><td>${usd(p.cacheReadPerMTok ?? p.inputPerMTok)} / ${usd(p.cacheWritePerMTok ?? p.inputPerMTok)}</td><td class="period-note"><p>${sourceLinks(p)}</p><small>Rate ${p.unconfirmed ? 'unconfirmed' : 'confirmed'} · checked ${esc(p.verifiedOn || 'not recorded')}</small><p>${p.validFromBasis?.includes('inference') ? '<strong>Historical start inferred from release.</strong>' : 'Provider-announced date.'} Day-level dates use 00:00 UTC.</p>${p.note ? '<p>' + esc(p.note) + '</p>' : ''}</td></tr>`).join('')}</tbody></table></div></details>`;
  function revealHash() {
    if (!location.hash.startsWith('#price-')) return;
    const target = document.getElementById(decodeURIComponent(location.hash.slice(1)));
    if (!target) return;
    let ancestor = target;
    while (ancestor) { if (ancestor instanceof HTMLDetailsElement) ancestor.open = true; ancestor = ancestor.parentElement; }
    requestAnimationFrame(() => target.scrollIntoView({ block:'start' }));
  }
  window.tokenEconomyPricingReady = fetch('../data/price-history.json').then(response => {
    if (!response.ok) throw new Error('Price data unavailable');
    return response.json();
  }).then(data => {
    const rows = [...data.models].sort((a,b) => (b.releaseDate || '').localeCompare(a.releaseDate || '') || a.modelId.localeCompare(b.modelId));
    const reference = document.getElementById('price-reference');
    reference.innerHTML = rows.map(row => `<option value="${esc(row.modelId)}" ${row.modelId === 'gpt-5.6-terra' ? 'selected' : ''}>${esc(row.displayName)}</option>`).join('');
    document.getElementById('price-coverage').innerHTML = `<span><strong>${data.coverage.pricedModels} / ${data.coverage.catalogModels}</strong> models with price history</span><span><strong>${data.coverage.pricePeriods}</strong> sourced price periods</span><span><strong>${esc(date(data.asOfUtc))}</strong> rate snapshot</span>`;
    const controls = document.getElementById('price-controls');
    const render = () => {
      const dateInput = document.getElementById('price-date');
      if (!dateInput.checkValidity()) {
        document.getElementById('price-basis').textContent = 'Choose a date within the researched period.';
        tableHost.innerHTML = '';
        return;
      }
      const at = dateInput.value + 'T12:00:00Z';
      const provider = document.getElementById('price-provider').value;
      const search = document.getElementById('price-search').value.toLowerCase().trim();
      const ref = rows.find(row => row.modelId === reference.value);
      const referenceAmount = amount(rateAt(ref, at));
      const matches = rows.filter(row => (!provider || row.vendor === provider) && (!search || `${row.modelId} ${row.displayName}`.toLowerCase().includes(search)));
      const active = matches.filter(row => !row.deprecated);
      const archived = matches.filter(row => row.deprecated);
      tableHost.innerHTML = (active.length ? table(active, at, referenceAmount) : '<p>No matching models in the active table.</p>')
        + (archived.length ? `<details class="pricing-archive"><summary>Archived models (${archived.length})</summary>${table(archived, at, referenceAmount)}</details>` : '');
      document.getElementById('price-basis').textContent = `${matches.length} matching models · ${dateInput.value} UTC · ` + (referenceAmount > 0 ? `${ref.displayName}: ${usd(referenceAmount)} = 1.00×. Values are API-equivalent USD.` : `${ref.displayName} has no recorded rate on this date; choose another reference to show the index.`);
    };
    controls.addEventListener('submit', event => event.preventDefault());
    controls.addEventListener('input', render);
    controls.addEventListener('change', render);
    render();
    historyHost.innerHTML = rows.filter(row => !row.deprecated).map(history).join('')
      + `<details class="pricing-archive"><summary>Archived model histories (${rows.filter(row => row.deprecated).length})</summary>${rows.filter(row => row.deprecated).map(history).join('')}</details>`;
    [tableHost, historyHost].forEach(host => host.removeAttribute('data-pending-content'));
    window.addEventListener('hashchange', revealHash);
    document.addEventListener('click', event => {
      const link = event.target.closest('a[href^="#price-"]');
      if (link && link.hash === location.hash) revealHash();
    });
    revealHash();
    document.dispatchEvent(new CustomEvent('site:content-ready'));
  }).catch(() => {
    document.getElementById('price-coverage').textContent = 'The price snapshot could not be loaded.';
    tableHost.innerHTML = '<p class="price-error">Prices are unavailable. <a href="../data/price-history.json">Open the price data</a> or reload this page.</p>';
    historyHost.textContent = 'History data is unavailable.';
    [tableHost, historyHost].forEach(host => host.removeAttribute('data-pending-content'));
    document.dispatchEvent(new CustomEvent('site:content-ready'));
  });
})();
