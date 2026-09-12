window.tokenEconomyAssessment = (() => {
  const esc = window.tokenEconomyEvidence.escape;
  const links = urls => (urls || []).map((url, i) => `<a href="${esc(url)}">Source ${i + 1}</a>`).join(' · ');
  const line = item => typeof item === 'string' ? esc(item) : `${esc(item.text)} ${links(item.sourceUrls)}`;
  function render(assessment, heading = 'h3') {
    if (!assessment) return '';
    return `<article class="model-assessment" id="assessment-${esc(assessment.modelId)}"><${heading}>${esc(assessment.displayName || assessment.modelId)}</${heading}><p>${esc(assessment.summary)}</p><details><summary>Evidence and effort guidance</summary>`
      + (assessment.strengths?.length ? `<h4>Where it performs well</h4><ul>${assessment.strengths.map(item => '<li>' + line(item) + '</li>').join('')}</ul>` : '')
      + (assessment.tradeoffs?.length ? `<h4>Tradeoffs</h4><ul>${assessment.tradeoffs.map(item => '<li>' + line(item) + '</li>').join('')}</ul>` : '')
      + (assessment.effortGuidance ? `<p><strong>Effort:</strong> ${line(assessment.effortGuidance)}</p>` : '')
      + (assessment.gaps?.length ? `<h4>Coverage and remaining checks</h4><ul>${assessment.gaps.map(item => '<li>' + line(item) + '</li>').join('')}</ul>` : '') + '</details></article>';
  }
  function revealHash() {
    if (!location.hash.startsWith('#assessment-')) return;
    const target = document.getElementById(decodeURIComponent(location.hash.slice(1)));
    if (!target) return;
    const detail = target.querySelector('details');
    if (detail) detail.open = true;
    requestAnimationFrame(() => target.scrollIntoView({block:'start'}));
  }
  window.addEventListener('hashchange', revealHash);
  document.addEventListener('click', event => {
    const anchor = event.target.closest('a[href]');
    if (anchor && anchor.origin === location.origin && anchor.pathname === location.pathname && anchor.hash === location.hash) revealHash();
  });
  return { render, revealHash };
})();
