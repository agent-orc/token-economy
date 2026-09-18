// Static-site projection of ContextSessionCost.cs; cross-language fixtures pin this recurrence.
export function resolvePrice(model, at) {
  return model?.history.filter(p => Date.parse(p.validFrom) <= at && (!p.validTo || at <= Date.parse(p.validTo)))
    .sort((a,b) => Date.parse(b.validFrom)-Date.parse(a.validFrom))[0] ?? null;
}
export function forecast(o, model) {
  if (o.cacheTtlMinutes == null) return { turns: [], total: null, unavailableReason: 'Cache TTL unknown. Enter an effective lifetime.' };
  let context=o.initialContext, cached=o.initiallyCachedTokens??0, at=Date.parse(o.startUtc), cumulative=0;
  const turns=[];
  const usage=(c,k,s,out) => o.cacheTtlMinutes===0 || c<o.minimumCacheTokens ? {input:c+s,output:out,cacheRead:0,cacheWrite:0} : {input:s,output:out,cacheRead:k>=o.minimumCacheTokens?Math.min(c,k):0,cacheWrite:c-(k>=o.minimumCacheTokens?Math.min(c,k):0)};
  const price=u => {
    const p=resolvePrice(model,at), write=o.cacheWritePerMTokOverride??p?.cacheWritePerMTok;
    if (!p || p.currency!=='USD' || (u.cacheRead>0 && p.cacheReadPerMTok==null) || (u.cacheWrite>0 && write==null)) return null;
    return {input:u.input*p.inputPerMTok/1e6,cacheRead:u.cacheRead*(p.cacheReadPerMTok??0)/1e6,cacheWrite:u.cacheWrite*(write??0)/1e6,output:u.output*p.outputPerMTok/1e6};
  };
  for(let i=0;i<o.turns;i++) {
    const gap=i===0?0:(o.gapsMinutes?.[i]??o.gapMinutes);
    at+=gap*60000;
    const cacheExpired=i>0 && gap>=o.cacheTtlMinutes;
    if(cacheExpired) cached=0;
    if(i>0 && o.restartEveryTurns>0 && i%o.restartEveryTurns===0) {context=o.restartContext;cached=Math.min(cached,o.sharedPrefixTokens);}
    const before=context, compacted=o.compactionThreshold>0 && context>=o.compactionThreshold;
    let cu={input:0,output:0,cacheRead:0,cacheWrite:0},cc={...cu};
    if(compacted) {cu=usage(context,cached,0,o.compactionOutputTokens);cc=price(cu);context=o.compactedContext;cached=0;at+=o.compactionMinutes*60000;}
    const u=usage(context,cached,o.inputTokensPerTurn,o.outputTokensPerTurn+o.reasoningTokensPerTurn), cost=price(u);
    const components=cost&&cc?Object.fromEntries(Object.keys(cost).map(k=>[k,cost[k]+cc[k]])):null;
    const total=components?Object.values(components).reduce((a,b)=>a+b,0):null;
    cumulative=cumulative==null||total==null?null:cumulative+total;
    turns.push({turn:i+1,atUtc:new Date(at).toISOString(),contextBeforeCompaction:before,contextTokens:context,compacted,cacheExpired,
      usage:Object.fromEntries(Object.keys(u).map(k=>[k,u[k]+cu[k]])),components,total,cumulativeCost:cumulative});
    cached=o.cacheTtlMinutes>0 && context>=o.minimumCacheTokens?context:0;
    context+=o.inputTokensPerTurn+o.toolTokensPerTurn+o.outputTokensPerTurn+(o.retainedReasoningTokensPerTurn??0);
  }
  return {turns,total:cumulative,unavailableReason:cumulative==null?'Price unavailable for one or more calls.':null};
}
export function crossing(a,b,perTask) {
  for(let i=perTask-1;i<Math.min(a.turns.length,b.turns.length);i+=perTask)
    if(a.turns[i].cumulativeCost!=null && b.turns[i].cumulativeCost!=null && a.turns[i].cumulativeCost>b.turns[i].cumulativeCost+1e-10) return (i+1)/perTask;
  return null;
}
export function scenarioOptions(p, modelId, policy, date='2026-09-18') {
  return {...p,modelId,startUtc:date+'T00:00:00Z',reasoningLevel:'medium',turns:p.tasks*p.turnsPerTask,
    cacheTtlMinutes:policy.ttlMinutes,minimumCacheTokens:policy.minimumCacheTokens,restartEveryTurns:0,
    gapsMinutes:Array.from({length:p.tasks*p.turnsPerTask},(_,i)=>i===0?0:(i%p.turnsPerTask===0?p.taskGapMinutes:p.gapMinutes))};
}
