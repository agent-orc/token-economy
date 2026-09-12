window.tokenEconomyEvidence = (() => {
  const escape = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
  const kinds = { benchmarkOwner:'Benchmark publisher', independentEvaluator:'Independent evaluator', officialLeaderboard:'Official leaderboard', modelProvider:'Model provider' };
  const number = value => Number(value).toLocaleString('en-US', { maximumFractionDigits:2 });
  function label(row) {
    return row.context?.dateBasis === 'firstObservedPublicSnapshot' ? 'Snapshot' : 'Published';
  }
  function score(row) {
    const context = row.context || {};
    return number(row.score) + ((context.confidenceIntervalHalfWidth ?? context.reportedErrorHalfWidth) != null ? ` ± ${number(context.confidenceIntervalHalfWidth ?? context.reportedErrorHalfWidth)}` : '');
  }
  function context(row) {
    const c = row.context;
    if (!c) return row.confidence === 'ownRun' ? 'Local controlled run' : row.confidence === 'publisherReported' ? 'Provider-reported result' : 'External result';
    const facts = [kinds[c.sourceKind] || c.sourceKind, c.sourcePublisher, c.harness ? `Harness: ${c.harness}${c.harnessVersion ? ' ' + c.harnessVersion : ''}` : null,
      c.taskCount != null ? `${c.taskCount} tasks` : null, c.trialCount != null ? `${c.trialCount} trials` : null,
      c.confidenceIntervalLevel != null ? `${number(c.confidenceIntervalLevel <= 1 ? c.confidenceIntervalLevel * 100 : c.confidenceIntervalLevel)}% confidence interval` : (c.confidenceIntervalHalfWidth ?? c.reportedErrorHalfWidth) != null ? 'Reported interval; confidence level unspecified' : null,
      c.runnerOrganization ? `Run submitted by ${c.runnerOrganization}` : null];
    return facts.filter(Boolean).join(' · ');
  }
  return { escape, label, score, context };
})();
