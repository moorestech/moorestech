// テスト用の使い捨て git 環境（origin・その clone・logs repo）
// Disposable git setup for tests: an origin, its clone, and a logs repo.

import { execFileSync } from "node:child_process";
import { mkdirSync, mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";

export function sh(cwd, cmd, ...args) {
  return execFileSync(cmd, args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
}

export function createRepos() {
  const root = mkdtempSync(join(tmpdir(), "logs-sync-test-"));
  const origin = join(root, "origin.git");
  const clone = join(root, "moorestech");
  const logs = join(root, "moorestech_logs");
  sh(root, "git", "init", "-q", "--bare", "-b", "master", origin);
  sh(root, "git", "clone", "-q", origin, clone);
  for (const dir of [clone]) {
    sh(dir, "git", "config", "user.email", "t@example.com");
    sh(dir, "git", "config", "user.name", "t");
  }
  mkdirSync(logs);
  sh(logs, "git", "init", "-q", "-b", "main");
  sh(logs, "git", "config", "user.email", "t@example.com");
  sh(logs, "git", "config", "user.name", "t");
  return { root, origin, clone, logs };
}

// clone にファイルを書いてコミットし、push まで済ませる
// Write a file in the clone, commit it and push.
export function commitFile(clone, path, content, { push = true } = {}) {
  const abs = join(clone, path);
  mkdirSync(dirname(abs), { recursive: true });
  writeFileSync(abs, content);
  sh(clone, "git", "add", path);
  sh(clone, "git", "commit", "-q", "-m", `add ${path}`);
  if (push) sh(clone, "git", "push", "-q", "origin", "HEAD:master");
  sh(clone, "git", "fetch", "-q", "origin");
  return abs;
}

// Read 1回分の subagent 記録（assistant の tool_use と user の tool_result）を作る
// Build one Read round trip of a subagent transcript (assistant tool_use and user tool_result).
export function readRoundTrip(id, filePath, content, startLine = 1) {
  const display = content.split("\n").map((line, i) => `${startLine + i}\t${line}`).join("\n");
  return [
    { type: "assistant", message: { content: [{ type: "tool_use", id, name: "Read", input: { file_path: filePath } }] } },
    {
      type: "user",
      message: { content: [{ type: "tool_result", tool_use_id: id, content: display }] },
      toolUseResult: { type: "text", file: { filePath, content, numLines: content.split("\n").length, startLine } },
    },
  ];
}

export function longText(seed, lines = 400) {
  return Array.from({ length: lines }, (_, i) => `${seed} line ${i} ${"x".repeat(20)}`).join("\n");
}
