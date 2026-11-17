// 同じ実行内の指摘（Critical と Warning）を保持し、再発の参照先と連続性を検査する。
// Keep findings (Critical and Warning) within this run and validate recurrence references and adjacency.
const refixHistory = []
const REFIX_REPORT_SCHEMA = {
  ...REPORT_SCHEMA,
  properties: { ...REPORT_SCHEMA.properties, findings: {
    type: 'array', items: { type: 'object', properties: {
      id: { type: 'string', minLength: 1 }, severity: { type: 'string', enum: ['Critical', 'Warning'] },
      summary: { type: 'string', minLength: 1 }, recurs_from: { type: ['string', 'null'] },
    }, required: ['id', 'severity', 'summary', 'recurs_from'] },
  } },
  required: [...REPORT_SCHEMA.required, 'findings'],
}
function trackRefix(result, round, history) {
  if (!Array.isArray(result.findings)) throw new Error(`Refix r${round}: findings が無い`)
  const ids = new Set()
  let stuck = false
  let promoted = 0
  for (const finding of result.findings) {
    if (!finding.id || ids.has(finding.id)) throw new Error(`Refix r${round}: id が空か重複`)
    ids.add(finding.id)
    if (finding.recurs_from !== null) {
      const previous = history.find((h) => h.round === finding.recurs_from && h.id === finding.id)
      if (!previous) {
        // 参照先が無い Warning は昇格させずログに残して続け、参照先が無い Critical は契約違反として止める。
        // A Warning with no reference is logged and kept; a Critical with no reference is a contract breach.
        if (finding.severity === 'Warning') {
          log(`Refix r${round}: Warning→Warning の再発は昇格しない ${finding.recurs_from}:${finding.id}`)
          finding.recurs_from = null
          continue
        }
        throw new Error(`Refix r${round}: 再発の参照先が無い ${finding.recurs_from}:${finding.id}`)
      }
      // 前周 Warning からの再発は今回の重さのまま扱い、昇格も stuck も数えない。
      // A recurrence of a prior Warning keeps its current severity and counts neither promotion nor stuck.
      if (previous.severity !== 'Critical') {
        log(`Refix r${round}: 前周 Warning からの再発は昇格しない ${finding.recurs_from}:${finding.id}`)
        finding.recurs_from = null
        continue
      }
      if (finding.severity === 'Warning') promoted++
      finding.severity = 'Critical'
      finding.summary += '（前回の修正が不完全な可能性）'
      if (previous.round === `r${round - 1}`) stuck = true
    }
  }
  // 件数と構造化指摘の食い違いを握り潰さず、親へエラーを返す。
  // Surface inconsistent counts to the parent instead of accepting an incomplete history.
  const count = result.findings.filter((f) => f.severity === 'Critical').length
  if (count !== result.critical_count + promoted) throw new Error(`Refix r${round}: Critical 件数と findings が不一致`)
  result.critical_count = count
  for (const f of result.findings) history.push({ round: `r${round}`, ...f })
  return { stuck, promoted }
}
