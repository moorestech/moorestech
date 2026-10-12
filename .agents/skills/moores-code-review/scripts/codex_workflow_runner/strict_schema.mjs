// =====================================================================
// ⚠ scripts/ 配下を変更したら必ず回帰テストを実行すること:
//     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
// Workflow のスキーマを codex exec --output-schema が受け付ける strict 形へ変換する。
// strict では全 object に additionalProperties:false と「全キー required」が要るため、元で任意だったキーは null 許容にして意味を保つ。
//
// ⚠ Run the regression suite after ANY change under scripts/.
// Converts Workflow schemas into the strict form codex exec --output-schema accepts:
// every object needs additionalProperties:false and all keys required, so originally optional keys become nullable.
// =====================================================================

export function toStrictSchema(schema) {
  if (!schema || typeof schema !== 'object') return schema
  const out = { ...schema }
  if (out.type === 'object' && out.properties) {
    // 元の required 外のキーは null 許容にしてから全キーを required にする
    // Make keys outside the original required list nullable, then require every key
    const required = new Set(out.required || [])
    const props = {}
    for (const [key, sub] of Object.entries(out.properties)) {
      const strictSub = toStrictSchema(sub)
      props[key] = required.has(key) ? strictSub : nullable(strictSub)
    }
    out.properties = props
    out.required = Object.keys(props)
    out.additionalProperties = false
  }
  if (out.type === 'array' && out.items) out.items = toStrictSchema(out.items)
  return out
}

function nullable(schema) {
  const out = { ...schema }
  const types = Array.isArray(out.type) ? out.type : [out.type]
  if (!types.includes('null')) out.type = [...types, 'null']
  if (Array.isArray(out.enum) && !out.enum.includes(null)) out.enum = [...out.enum, null]
  return out
}
