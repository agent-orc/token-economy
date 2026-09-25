/* Render catalogue values as text, including imported notes and study metadata. */
'use strict';
const $ = id => document.getElementById(id);
const node = (tag, value, className) => {
  const element = document.createElement(tag);
  if (value !== undefined) element.textContent = value;
  if (className) element.className = className;
  return element;
};
const number = (value, digits = 2) => value === null ? '—' : Number(value).toFixed(digits);
const link = (label, url) => {
  const element = node('a', label);
  if (url.startsWith('#') || /^https:\/\//.test(url)) element.href = url;
  return element;
};
function tableRow(values) {
  const row = node('tr');
  for (const [index, value] of values.entries()) {
    const cell = node('td');
    if (index === 0) cell.className = 'route';
    cell.append(value instanceof Node ? value : document.createTextNode(String(value)));
    row.append(cell);
  }
  return row;
}
function evidenceLink(record) {
  const element = link(record.status, `#record-${record.id}`);
  const date = record.evidence.map(e => e.observedAtUtc).sort().at(-1).slice(0, 10);
  const time = node('time', date); time.dateTime = date;
  element.append(node('br'), time);
  return element;
}
async function start() {
  const response = await fetch('../data/language-capabilities.json');
  if (!response.ok) throw new Error(`Evidence request failed (${response.status})`);
  const data = await response.json();
  $('model-count').textContent = new Set(data.records.map(r => r.modelId)).size;
  $('observed-count').textContent = data.records.filter(r => r.status === 'observed').length;
  $('study-count').textContent = data.studies.length;
  $('load-status').textContent = `Evidence reviewed through ${data.asOfDate}.`;
  const selected = new Set(data.selectedRecordIds);
  const rows = data.records.filter(r => selected.has(r.id));
  function render() {
    const visible = r => ($('language').value === 'all' || r.language === $('language').value)
      && ($('thinking').value === 'all' || r.thinkingLevel === $('thinking').value);
    $('matrix-body').replaceChildren(...rows.filter(visible).map(r => tableRow([
      r.modelId, `${r.language} / ${r.thinkingLevel}`,
      ...data.dimensions.map(d => number(r.scores[d])), number(r.overall), number(r.costPerSampleUsd, 6),
      evidenceLink(r),
    ])));
    const input = $('threshold');
    const threshold = input.valueAsNumber;
    const valid = input.value !== '' && input.checkValidity() && Number.isFinite(threshold);
    const passing = data.costQuality.filter(r => visible(r) && valid && r.overall !== null
      && r.overall >= threshold && r.costPerSampleUsd !== null
      && (r.status === 'observed' || ($('allow-provisional').checked && r.status === 'provisional')))
      .sort((a, b) => a.costPerSampleUsd - b.costPerSampleUsd);
    $('quality-status').textContent = !valid ? 'Enter a threshold from 0 to 1.' : passing.length
      ? `${passing.length} priced measurements clear this overall threshold. Check dimension floors and comparable methods before routing.`
      : 'No priced measurements clear this threshold. Unknown scores and costs cannot establish a cheapest model.';
    $('cost-body').replaceChildren(...passing.map(r => tableRow([r.modelId, `${r.language} / ${r.thinkingLevel}`,
      number(r.overall), number(r.costPerSampleUsd, 6), number(r.costPerQualityPointUsd, 6),
      link(r.status, `#record-${r.recordId}`)])));
  }
  for (const id of ['language', 'thinking', 'threshold', 'allow-provisional']) $(id).addEventListener('input', render);
  $('priors').replaceChildren(...data.languagePriors.map(p => node('li', `${p.textKind} / ${p.language}: ${p.candidates.length
    ? p.candidates.map(c => `${c.modelId} (${c.evidenceStatus}, $${number(c.costPerSampleUsd, 6)}/sample)`).join(', ')
    : 'no evidence-qualified route yet'}`)));
  $('study-list').replaceChildren(...data.studies.map(s => {
    const card = node('article', undefined, 'card');
    const heading = node('h3'); heading.append(link(s.title, s.url));
    card.append(heading, node('p', `${s.authors} · ${s.year} · ${s.kind}`, 'muted'),
      node('p', s.claim), node('p', s.relevance, 'sub'),
      node('p', `${s.status} · ${s.verifiedAtUtc ? `verified ${s.verifiedAtUtc.slice(0, 10)}` : 'source not independently verified'}`, 'evidence'));
    return card;
  }));
  $('evidence-list').replaceChildren(...data.records.map(r => {
    const details = node('details', undefined, 'card evidence'); details.id = `record-${r.id}`;
    details.append(node('summary', `${r.modelId} · ${r.language} · ${r.thinkingLevel} · ${r.status}`), node('p', r.notes));
    const list = node('ul');
    for (const e of r.evidence) {
      const entry = node('li');
      entry.append(node('span', `${e.kind} · ${e.observedAtUtc} · `));
      // A local run path belongs to Voice Lint, never to this website's root.
      entry.append(e.kind === 'study' ? link(e.reference, e.reference) : node('code', e.reference));
      entry.append(node('p', e.note)); list.append(entry);
    }
    details.append(list);
    if (r.provenance) details.append(node('p', `Source: ${r.provenance.sourceRepository} / ${r.provenance.sourcePath}`),
      node('p', `SHA-256: ${r.provenance.sourceSha256}`),
      link('Mirrored result file', `https://github.com/agent-orc/token-economy/blob/main/${r.provenance.mirrorPath}`));
    return details;
  }));
  render();
  function revealEvidence() {
    const target = document.getElementById(location.hash.slice(1));
    if (target?.tagName === 'DETAILS') { target.open = true; target.scrollIntoView(); }
  }
  window.addEventListener('hashchange', revealEvidence);
  revealEvidence();
}
start().catch(error => { $('load-status').textContent = `Language evidence could not load. ${error.message}. Try the JSON download below.`; });
