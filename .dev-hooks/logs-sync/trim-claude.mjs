// Claude の subagent 記録 1 行から、元に戻せる部分だけを目印へ置き換える
// Replace only the restorable parts of one Claude subagent transcript line with markers.
//
// 削るもの / What gets trimmed:
//   - Read で読んだファイル内容（消えないコミットに同じ内容がある時だけ）/ file content read via Read, when a durable commit holds it
//   - コミットに無い Read 結果は、行番号つき表示と生テキストの二重記録の片方 / otherwise, the raw copy duplicating the numbered display
//   - Edit/Write 前のファイル全文（同上）/ the pre-edit file body, same condition
//   - toolUseResult 内で tool_result と同じ文字列（二重記録）/ toolUseResult strings duplicating the tool_result
//   - 読んだ画像の base64 を blobs/ へ1回だけ / image base64 from Read, stored once in blobs/
//   - 添付・API要求の大きな定型文（システムプロンプト等）を blobs/ へ1回だけ / large boilerplate in attachments and API requests, stored once in blobs/

import { MARKER_KEY, parseCatN, renderCatN } from "./trim-marker.mjs";

// 二重記録や定型文として扱う最小の長さ（短い文字列は目印の方が長くなる）
// Minimum length worth replacing; for short strings the marker itself would be longer.
const SAME_MIN_CHARS = 1024;
const BLOB_MIN_CHARS = 4096;
const BOILERPLATE_LINE_TYPES = new Set(["attachment", "api-request-blob", "api-request-shape"]);

// state.tools: tool_use id → {name, input}（tool_result は後の行に来るので行をまたいで覚える）
// state.tools remembers tool_use id → {name, input} because the tool_result arrives on a later line.
export function trimClaudeLine(line, state, ctx) {
  if (line.type === "assistant") rememberToolUses(line, state);
  if (BOILERPLATE_LINE_TYPES.has(line.type)) return replaceLongStrings(line, ctx);
  if (line.type !== "user" || !Array.isArray(line.message?.content)) return line;
  const out = structuredClone(line);
  out.message.content.forEach((block, index) => {
    if (block?.type !== "tool_result") return;
    const tool = state.tools.get(block.tool_use_id);
    if (tool?.name === "Read") trimReadResult(out, block, index, tool, ctx);
  });
  trimOriginalFile(out, ctx);
  replaceDuplicates(out);
  storeImagesOnce(out, ctx);
  return out;
}

function rememberToolUses(line, state) {
  for (const block of line.message?.content ?? []) {
    if (block?.type === "tool_use") state.tools.set(block.id, { name: block.name, input: block.input ?? {} });
  }
}

// Read の表示（行番号つき）と toolUseResult.file.content を、同じコミット位置への目印にする
// Point both the numbered display and toolUseResult.file.content at the same commit slice.
function trimReadResult(line, block, index, tool, ctx) {
  if (typeof block.content !== "string") return;
  const file = line.toolUseResult?.file;
  const path = file?.filePath ?? tool.input.file_path;
  const parsed = file && typeof file.content === "string"
    ? { text: file.content, startLine: file.startLine ?? 1 }
    : parseCatN(block.content);
  if (!path || !parsed || renderCatN(parsed.text, parsed.startLine) !== block.content) return;
  const ref = ctx.locate(path, parsed.text);
  if (ref) {
    line.message.content[index] = { ...block, content: { ...ref, catn: parsed.startLine } };
    if (file?.content === parsed.text) file.content = { ...ref };
    return;
  }
  // コミットに無い内容でも、生テキストは行番号つき表示から必ず作り直せるので片方だけ残す
  // Even without a commit, the raw text is always rebuildable from the numbered display, so keep only that.
  if (file?.content === parsed.text) file.content = { [MARKER_KEY]: "uncatn", at: ["message", "content", index, "content"] };
}

function trimOriginalFile(line, ctx) {
  const result = line.toolUseResult;
  if (typeof result?.originalFile !== "string" || !result.filePath) return;
  const ref = ctx.locate(result.filePath, result.originalFile);
  if (ref && ref.offset === 0) result.originalFile = { ...ref };
}

// toolUseResult 内の長い文字列が tool_result 本文と同じなら、その場所を指す目印にする
// Replace long toolUseResult strings that equal a tool_result body with a pointer to it.
function replaceDuplicates(line) {
  if (line.toolUseResult === null || typeof line.toolUseResult !== "object") return;
  const bodies = new Map();
  line.message.content.forEach((block, i) => {
    if (block?.type !== "tool_result") return;
    if (typeof block.content === "string") bodies.set(block.content, ["message", "content", i, "content"]);
    if (Array.isArray(block.content)) {
      block.content.forEach((part, j) => {
        if (typeof part?.text === "string") bodies.set(part.text, ["message", "content", i, "content", j, "text"]);
        if (typeof part?.source?.data === "string") bodies.set(part.source.data, ["message", "content", i, "content", j, "source", "data"]);
      });
    }
  });
  walkStrings(line.toolUseResult, (text) => {
    const at = text.length >= SAME_MIN_CHARS ? bodies.get(text) : undefined;
    return at ? { [MARKER_KEY]: "same", at } : text;
  });
}

// 読んだ画像の base64 は、並列レビューの agent 同士で同じものを何度も読むので blobs/ に1回だけ置く
// Image base64 from Read is re-read by many parallel review agents, so store each image once in blobs/.
function storeImagesOnce(line, ctx) {
  for (const block of line.message.content) {
    if (block?.type !== "tool_result" || !Array.isArray(block.content)) continue;
    for (const part of block.content) {
      const data = part?.type === "image" ? part.source?.data : undefined;
      if (typeof data === "string" && data.length >= BLOB_MIN_CHARS) part.source.data = { [MARKER_KEY]: "blob", sha256: ctx.writeBlob(data) };
    }
  }
}

function replaceLongStrings(line, ctx) {
  const out = structuredClone(line);
  walkStrings(out, (text) => (text.length >= BLOB_MIN_CHARS ? { [MARKER_KEY]: "blob", sha256: ctx.writeBlob(text) } : text));
  return out;
}

// オブジェクト内の文字列を fn の戻り値で置き換える（その場で書き換える）
// Replace every string inside node, in place, with fn(string).
function walkStrings(node, fn) {
  const entries = Array.isArray(node) ? node.entries() : Object.entries(node);
  for (const [key, value] of entries) {
    if (typeof value === "string") node[key] = fn(value);
    else if (value !== null && typeof value === "object") walkStrings(value, fn);
  }
}
