// 自動コミットから動画・大きなファイル・一括コピーが外れ、初回だけ知らされることを確かめる
// Videos, large files and bulk copies are kept out of the auto-commit and reported only once.

import assert from "node:assert/strict";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { test } from "node:test";
import { unstageRejected } from "../intake-guard.mjs";
import { createRepos, sh } from "./fixture.mjs";

test("raw data is unstaged and reported once; records and conversation logs are committed", () => {
  const { logs } = createRepos();
  const state = join(logs, ".state");
  mkdirSync(state);
  writeFileSync(join(logs, ".gitignore"), ".state/\n");
  const write = (path, content) => {
    mkdirSync(join(logs, path, ".."), { recursive: true });
    writeFileSync(join(logs, path), content);
  };
  write("harness/sdd/run/report.md", "# 結論\n");
  write("harness/sdd/run/recording.mp4", "fake video");
  write("harness/playtest/r1/snapshots/tick_1.json", "x".repeat(3 * 1024 * 1024));
  for (let i = 0; i < 250; i++) write(`harness/tier9/copy/f${i}.txt`, `${i}`);
  write("claude/slug/big.jsonl", "y".repeat(3 * 1024 * 1024));

  const reports = [];
  sh(logs, "git", "add", "-A");
  unstageRejected(logs, state, (line) => reports.push(line));

  const staged = sh(logs, "git", "diff", "--cached", "--name-only").split("\n").sort();
  assert.deepEqual(staged, [".gitignore", "claude/slug/big.jsonl", "harness/sdd/run/report.md"]);
  assert.equal(reports.length, 3);
  assert.ok(reports.some((l) => l.includes("rejected (video): harness/sdd/run/recording.mp4")));
  assert.ok(reports.some((l) => l.includes("harness/playtest/r1/snapshots/tick_1.json")));
  assert.ok(reports.some((l) => l.includes("bulk: 250 new files under harness/tier9/copy")));

  sh(logs, "git", "commit", "-q", "-m", "first");
  sh(logs, "git", "add", "-A");
  unstageRejected(logs, state, (line) => reports.push(line));
  assert.equal(reports.length, 3);
  assert.equal(sh(logs, "git", "diff", "--cached", "--name-only"), "");
});
