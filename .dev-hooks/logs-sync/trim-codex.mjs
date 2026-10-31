// Codex の自動実行（codex exec）記録 1 行から、ファイルを読んだだけのコマンド出力を目印へ置き換える
// Replace the output of read-only commands in one Codex exec rollout line with a git marker.
//
// 他のコマンド出力は後から作り直せないので削らない
// Other command output cannot be regenerated later, so it is kept as is.

import { isAbsolute, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const MIN_CHARS = 1024;
const OUTPUT_KEYS = ["aggregated_output", "stdout"];

export function trimCodexLine(line, ctx) {
  const item = line.payload?.type === "item_completed" ? line.payload.item : null;
  if (item?.type !== "CommandExecution") return line;
  const path = singleReadPath(item);
  if (!path) return line;
  const out = structuredClone(line);
  for (const key of OUTPUT_KEYS) {
    const text = out.payload.item[key];
    if (typeof text !== "string" || text.length < MIN_CHARS) continue;
    const ref = ctx.locate(path, text);
    if (ref) out.payload.item[key] = { ...ref };
  }
  return out;
}

// parsed_cmd がすべて同じ1ファイルの read の時だけ、その絶対パスを返す
// Return the absolute path only when every parsed_cmd entry is a read of the same single file.
function singleReadPath(item) {
  const parsed = item.parsed_cmd;
  if (!Array.isArray(parsed) || parsed.length === 0) return null;
  if (!parsed.every((cmd) => cmd?.type === "read" && typeof cmd.path === "string")) return null;
  const paths = new Set(parsed.map((cmd) => cmd.path));
  if (paths.size !== 1) return null;
  const [path] = paths;
  if (isAbsolute(path)) return path;
  const cwd = toDirPath(item.cwd);
  return cwd ? resolve(cwd, path) : null;
}

function toDirPath(cwd) {
  if (typeof cwd !== "string") return null;
  return cwd.startsWith("file://") ? fileURLToPath(cwd) : cwd;
}
