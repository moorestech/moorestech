# .claude/skills/moores-code-review/tests/workflow_sim.py
# 結合済み Workflow スクリプト（args 埋め込み済み）を node で模擬実行する補助。Workflow ランタイムと同じく
# 本体を async 関数本体として評価し、agent() はスキーマの required を既定値で埋めた応答を返す。
# 構文だけでなく、埋め込んだ args で全フェーズの参照が解決し return まで届くことを確かめる。
# Helper that runs an assembled (args-embedded) workflow script under node with mocked hooks, evaluating the
# body as an async function body like the Workflow runtime; agent() answers with schema-shaped defaults.
import json
import shutil
import subprocess
import tempfile
from pathlib import Path

HARNESS = r"""
const calls = []
// ラベル接頭辞→応答の上書き（Refix の直し直し経路など既定値では通らない分岐を踏ませる）
// Label-prefix -> response overrides, to drive branches the defaults never reach (e.g. the Refix re-fix path)
const OVERRIDES = __OVERRIDES__
const fill = (schema) => {
  if (!schema) return 'ok'
  if (schema.enum) return schema.enum[0]
  if (schema.type === 'object') {
    const o = {}
    for (const k of schema.required || []) o[k] = fill(schema.properties[k])
    return o
  }
  if (schema.type === 'array') return []
  if (schema.type === 'integer' || schema.type === 'number') return 0
  if (schema.type === 'boolean') return true
  return 'ok'
}
const agent = async (prompt, opts) => {
  if (!opts || !opts.model) throw new Error('model 未指定の agent(): ' + String(prompt).slice(0, 60))
  calls.push({ label: opts.label, phase: opts.phase, model: opts.model, schema: opts.schema || null, prompt })
  const answer = fill(opts.schema)
  for (const [prefix, patch] of Object.entries(OVERRIDES)) if (opts.label.startsWith(prefix)) Object.assign(answer, patch)
  return answer
}
const parallel = async (thunks) => Promise.all(thunks.map((t) => t().catch(() => null)))
const log = () => {}
const phase = () => {}
const args = undefined
const body = async () => {
__BODY__
}
body().then((r) => console.log(JSON.stringify({ result: r, calls })))
  .catch((e) => { console.error(String(e && e.stack || e)); process.exit(3) })
"""


def node_path():
    return shutil.which("node")


def simulate(script_path: Path, overrides: dict | None = None) -> dict:
    # meta の export は関数本体内では書けないので、ランタイム同様に剥がしてから本体として包む
    # `export` is illegal inside a function body, so strip it (as the runtime does) before wrapping
    src = script_path.read_text(encoding="utf-8").replace("export const meta", "const meta", 1)
    with tempfile.NamedTemporaryFile("w", suffix=".mjs", delete=False, encoding="utf-8") as fh:
        fh.write(HARNESS.replace("__OVERRIDES__", json.dumps(overrides or {})).replace("__BODY__", src))
        path = fh.name
    run = subprocess.run([node_path(), path], capture_output=True, text=True, timeout=60)
    if run.returncode != 0:
        raise AssertionError(f"Workflow 模擬実行が失敗: {run.stderr[:800]}")
    return json.loads(run.stdout)
