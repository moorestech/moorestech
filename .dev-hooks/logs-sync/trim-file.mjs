// 自動実行の記録ファイルを、元に戻せる部分だけ削って書き出す。各行は削った後に復元して一致した時だけ採用する
// Write an automated-run transcript with only its restorable parts trimmed; each line is adopted only if it restores exactly.

import { existsSync, mkdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { dirname, join, sep } from "node:path";
import { createGitSource, sha256 } from "./git-source.mjs";
import { trimClaudeLine } from "./trim-claude.mjs";
import { trimCodexLine } from "./trim-codex.mjs";
import { restoresTo } from "./trim-marker.mjs";

// logs repo 内で定型文の本文を1回だけ置く場所
// Where boilerplate bodies are stored once inside the logs repo.
export function blobPath(logsRepo, hash) {
  return join(logsRepo, "blobs", hash.slice(0, 2), `${hash}.txt`);
}

// 1回の同期で共有する文脈（git の記憶と blobs の読み書き）
// Context shared across one sync run: memoised git access and blob read/write.
export function createTrimContext(logsRepo) {
  const pending = new Map();
  const git = createGitSource();
  return {
    git,
    // 消えないコミットか、logs repo に退避済みの Claude 出力ファイルの中に text があれば、その場所の目印を返す
    // Marker for text found in a durable commit or in a Claude output file already mirrored into the logs repo.
    locate(absPath, text) {
      return git.locate(absPath, text) ?? locateInLogs(logsRepo, absPath, text);
    },
    readLogsFile: (path) => readFileSync(join(logsRepo, path), "utf8"),
    writeBlob(text) {
      const hash = sha256(text);
      if (!pending.has(hash)) pending.set(hash, text);
      return hash;
    },
    readBlob(hash) {
      return pending.has(hash) ? pending.get(hash) : readVerifiedBlob(logsRepo, hash);
    },
    // 採用された行が参照する本文だけを書く（検証で落ちた行の本文は置かない）
    // Persist only bodies referenced by adopted lines (bodies from rejected lines are not written).
    flushBlobs(used) {
      for (const hash of used) {
        const path = blobPath(logsRepo, hash);
        // 途中で切れた既存の本文（容量不足など）は名前が合っていても中身で見分けて書き直す
        // A truncated existing body (e.g. disk full) is detected by content, not by name, and rewritten.
        if (existsSync(path) && sha256(readFileSync(path, "utf8")) === hash) continue;
        mkdirSync(dirname(path), { recursive: true });
        writeFileSync(`${path}.tmp`, pending.get(hash));
        renameSync(`${path}.tmp`, path);
      }
    },
  };
}

// kind: "claude"（subagent 記録）か "codex"（codex exec 記録）。戻り値は削った行数
// kind is "claude" (subagent transcript) or "codex" (codex exec rollout); returns the number of trimmed lines.
export function writeTrimmedTranscript(src, dest, kind, ctx) {
  const state = { tools: new Map() };
  const used = new Set();
  let trimmedLines = 0;
  const lines = readFileSync(src, "utf8").split("\n").map((raw) => {
    const original = parseLine(raw);
    if (original === null) return raw;
    const trimmed = kind === "claude" ? trimClaudeLine(original, state, ctx) : trimCodexLine(original, ctx);
    if (trimmed === original) return raw;
    const text = JSON.stringify(trimmed);
    if (text.length >= raw.length || !restoresTo(trimmed, original, ctx)) return raw;
    collectBlobHashes(trimmed, used);
    trimmedLines += 1;
    return text;
  });
  ctx.flushBlobs(used);
  mkdirSync(dirname(dest), { recursive: true });
  writeFileSync(dest, lines.join("\n"));
  return trimmedLines;
}

// 本文を読み、名前のハッシュと中身が一致しなければ理由つきで投げる
// Read a blob body and throw with the reason when its content does not match its name.
export function readVerifiedBlob(logsRepo, hash) {
  const text = readFileSync(blobPath(logsRepo, hash), "utf8");
  if (sha256(text) !== hash) throw new Error(`blob content does not match its sha256: blobs/${hash.slice(0, 2)}/${hash}.txt`);
  return text;
}

// ~/.claude/projects/<slug>/.../tool-results/ は logs repo の claude/<slug>/... に複製されている（退避済みの時だけ使う）。
// tool-results は一度書かれたら変わらないので参照先にできる。削って書き直される subagent 記録などは参照先にしない
// ~/.claude/projects/<slug>/.../tool-results/ is mirrored at claude/<slug>/... in the logs repo; used once the copy exists.
// tool-results are write-once, so they are safe targets; files that get rewritten (e.g. trimmed subagent transcripts) are not.
function locateInLogs(logsRepo, absPath, text) {
  const projects = join(homedir(), ".claude", "projects") + sep;
  if (!absPath.startsWith(projects) || !absPath.includes(`${sep}tool-results${sep}`)) return null;
  const path = join("claude", absPath.slice(projects.length));
  if (!existsSync(join(logsRepo, path))) return null;
  const offset = readFileSync(join(logsRepo, path), "utf8").indexOf(text);
  if (offset < 0 || text.length === 0) return null;
  return { $trim: "logs", path, offset, length: text.length, sha256: sha256(text) };
}

function parseLine(raw) {
  if (raw.trim() === "") return null;
  // silent-ok: JSON でない行は削らず原文のまま保存するので記録は欠けない / 欠落が無いため通知は不要
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

function collectBlobHashes(node, used) {
  if (node === null || typeof node !== "object") return;
  if (node.$trim === "blob") used.add(node.sha256);
  for (const value of Object.values(node)) collectBlobHashes(value, used);
}
