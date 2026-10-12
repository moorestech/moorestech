// ---- 構造化出力のスキーマ（各 agent の返答は件数と短い申告だけ。本文はファイルへ）----
// ---- Structured-output schemas: agents return counts and short notes only; bodies go to files ----

const REPORT_SCHEMA = {
  type: 'object',
  properties: {
    critical_count: { type: 'integer' },
    design_judgement: { type: 'boolean' },
    summary: { type: 'string' },
    report_written: { type: 'boolean' },
  },
  required: ['critical_count', 'design_judgement', 'summary', 'report_written'],
}
const CODEX_WAIT_SCHEMA = {
  type: 'object',
  properties: {
    results: {
      type: 'array',
      items: { type: 'object', properties: { name: { type: 'string' }, exit_code: { type: 'integer' }, status: { type: 'string' } }, required: ['name', 'exit_code', 'status'] },
    },
    waited_minutes: { type: 'number' },
  },
  required: ['results', 'waited_minutes'],
}
// bug-pass は設計判断を出さない（AskUserQuestion を作らない）。1件でも返せばスキーマ検証で integrator がやり直す
// bug-pass never yields design items (no AskUserQuestion); any non-zero count fails schema validation and the integrator retries
const INTEGRATOR_SCHEMA = {
  type: 'object',
  properties: {
    critical: { type: 'integer' }, warning: { type: 'integer' }, info: { type: 'integer' },
    suppressed: { type: 'integer' },
    design_items: BUG_PASS ? { type: 'integer', minimum: 0, maximum: 0 } : { type: 'integer' },
    missing_systems: { type: 'array', items: { type: 'string' } },
    integrated_written: { type: 'boolean' },
  },
  required: ['critical', 'warning', 'info', 'suppressed', 'design_items', 'missing_systems', 'integrated_written'],
}
const APPLY_SCHEMA = {
  type: 'object',
  properties: {
    applied: { type: 'integer' },
    compile: { type: 'string', enum: ['ok', 'error', 'skipped'] },
    tests: { type: 'string' },
    design_items: { type: 'integer' },
    post_checks: {
      type: 'array',
      items: { type: 'object', properties: { path: { type: 'string' }, model: { type: 'string' } }, required: ['path', 'model'] },
    },
    post_check_selection_note: { type: 'string' },
    // 反映 diff の scope（refix_snapshot.py の出力）。error は diff を作れなかった申告で、親は止まる
    // Scope of the applied diff (from refix_snapshot.py); 'error' means it could not be built and the parent stops
    refix_scope: { type: 'string', enum: ['source', 'non-source', 'none', 'error'] },
    refix_note: { type: 'string' },
    // 機械的動作確認（録画シナリオ追従・セーブ往復テスト）の結果1行。「未確認」も理由付きでここへ書く
    // One-line result of the mechanical checks (playtest scenarios, save/load round trip); 'unverified' goes here with a reason
    verify_note: { type: 'string' },
    notes: { type: 'string' },
  },
  required: ['applied', 'compile', 'design_items', 'post_checks', 'post_check_selection_note', 'refix_scope', 'refix_note', 'verify_note'],
}
const POSTFIX_SCHEMA = {
  type: 'object',
  properties: {
    applied: { type: 'integer' }, escalated: { type: 'integer' },
    compile: { type: 'string', enum: ['ok', 'error', 'skipped'] },
    warnings: { type: 'array', items: { type: 'string' } },
    infos: { type: 'array', items: { type: 'string' } },
    notes: { type: 'string' },
  },
  required: ['applied', 'escalated', 'compile', 'warnings', 'infos'],
}
const REFIX_APPLY_SCHEMA = {
  type: 'object',
  properties: {
    applied: { type: 'integer' }, escalated: { type: 'integer' },
    compile: { type: 'string', enum: ['ok', 'error', 'skipped'] },
    refix_scope: { type: 'string', enum: ['source', 'non-source', 'none', 'error'] },
    refix_note: { type: 'string' },
    verify_note: { type: 'string' },
    warnings: { type: 'array', items: { type: 'string' } },
    infos: { type: 'array', items: { type: 'string' } },
    notes: { type: 'string' },
  },
  required: ['applied', 'escalated', 'compile', 'refix_scope', 'refix_note', 'verify_note', 'warnings', 'infos'],
}
