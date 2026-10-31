#!/usr/bin/env node
// 削って保存した記録を元の内容へ戻して標準出力へ書く
// Restore a trimmed transcript to its original content and write it to stdout.
//
//   node .dev-hooks/logs-sync/restore-transcript.mjs <logs repo 内の .jsonl> [--repo <clone>]...
//
// 記録した worktree が消えていても、同じ origin を持つ clone（既定: logs repo の兄弟 moorestech と --repo）から取り出す。
// 取り出せない目印が1つでもあれば、行番号と理由を出して exit 1（欠けたまま出力しない）。
// When the recorded worktree is gone, content comes from any clone with the same origin (default: the sibling moorestech plus --repo).
// Any unrestorable marker aborts with its line number and reason (exit 1); partial output is never presented as complete.

import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { createGitSource } from "./git-source.mjs";
import { blobPath } from "./trim-file.mjs";
import { restoreLine } from "./trim-marker.mjs";

const args = process.argv.slice(2);
const file = args.find((a, i) => !a.startsWith("--") && args[i - 1] !== "--repo");
if (!file) {
  console.error("usage: restore-transcript.mjs <trimmed.jsonl> [--repo <clone>]...");
  process.exit(2);
}
const logsRepo = findLogsRepo(resolve(file));
const clones = [
  ...args.filter((_, i) => args[i - 1] === "--repo").map((p) => resolve(p)),
  join(dirname(logsRepo), "moorestech"),
].filter((p) => existsSync(p));
const ctx = {
  git: createGitSource(),
  readBlob: (hash) => readFileSync(blobPath(logsRepo, hash), "utf8"),
  readLogsFile: (path) => readFileSync(join(logsRepo, path), "utf8"),
  repoFor: (marker) => (existsSync(marker.repo) ? marker.repo : cloneWithUrl(marker.url)),
};

const out = [];
for (const [index, raw] of readFileSync(file, "utf8").split("\n").entries()) {
  if (!raw.includes('"$trim"')) {
    out.push(raw);
    continue;
  }
  try {
    out.push(JSON.stringify(restoreLine(JSON.parse(raw), ctx)));
  } catch (error) {
    console.error(`restore failed at line ${index + 1}: ${error.message}`);
    console.error("hint: git -C <clone> fetch origin で該当コミットを取り込むか、--repo で clone を指定する");
    process.exit(1);
  }
}
process.stdout.write(out.join("\n"));

// 記録ファイルから上へ辿って blobs/ を持つ logs repo を見つける
// Walk up from the transcript to the logs repo that owns blobs/.
function findLogsRepo(path) {
  for (let dir = dirname(path); dir !== dirname(dir); dir = dirname(dir)) {
    if (existsSync(join(dir, ".git"))) return dir;
  }
  console.error(`logs repo not found above ${path}`);
  process.exit(2);
}

function cloneWithUrl(url) {
  const found = clones.find((clone) => normalizeUrl(originOf(clone)) === normalizeUrl(url));
  if (!found) throw new Error(`no local clone with origin ${url} (pass --repo <clone>)`);
  return found;
}

function originOf(clone) {
  // silent-ok: origin を読めない clone は候補から外れるだけで、候補が尽きれば cloneWithUrl が理由つきで止める / 実行者が端末で読む
  try {
    return execFileSync("git", ["-C", clone, "remote", "get-url", "origin"], { encoding: "utf8" }).trim();
  } catch {
    return "";
  }
}

// git@github.com:a/b.git と https://github.com/a/b を同じものとして比べる
// Treat git@github.com:a/b.git and https://github.com/a/b as the same remote.
function normalizeUrl(url) {
  return (url ?? "").replace(/^git@([^:]+):/, "https://$1/").replace(/\.git$/, "").toLowerCase();
}
