// 自動コミットから記録でない物（コピー・動画・大きなファイル・連番画像）が外れ、初回だけ知らされることを確かめる
// Non-records (copies, videos, large files, image sequences) are kept out of the auto-commit and reported only once.

import assert from "node:assert/strict";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { test } from "node:test";
import { unstageRejected } from "../intake-guard.mjs";
import { createRepos, sh } from "./fixture.mjs";

function setup() {
  const { logs } = createRepos();
  const state = join(logs, ".state");
  mkdirSync(state);
  writeFileSync(join(logs, ".gitignore"), ".state/\n");
  const write = (path, content = "x") => {
    mkdirSync(dirname(join(logs, path)), { recursive: true });
    writeFileSync(join(logs, path), content);
  };
  const reports = [];
  const sync = () => {
    sh(logs, "git", "add", "-A");
    unstageRejected(logs, state, (line) => reports.push(line));
    const staged = sh(logs, "git", "diff", "--cached", "--name-only").split("\n").filter(Boolean).sort();
    if (staged.length > 0) sh(logs, "git", "commit", "-q", "-m", "sync");
    return staged;
  };
  return { write, sync, reports };
}

test("copies, videos and large files are unstaged per file; neighbouring records still commit", () => {
  const { write, sync, reports } = setup();
  write("harness/sdd/run/report.md", "# 結論\n");
  write("harness/sdd/run/recording.mp4");
  write("harness/playtest/r1/snapshots/tick_1.json", "x".repeat(3 * 1024 * 1024));
  for (let i = 0; i < 250; i++) write(`harness/moores-code-review/runs/r1/refix/F${i}.cs`);
  write("harness/moores-code-review/runs/r1/report.md", "# r1\n");
  write("claude/slug/big.jsonl", "y".repeat(3 * 1024 * 1024));

  assert.deepEqual(sync(), [
    ".gitignore", "claude/slug/big.jsonl", "harness/moores-code-review/runs/r1/report.md", "harness/sdd/run/report.md",
  ]);
  assert.equal(reports.length, 3);
  assert.ok(reports.some((l) => l.includes("rejected (not a record type: .mp4): harness/sdd/run/recording.mp4")));
  assert.ok(reports.some((l) => l.includes("harness/playtest/r1/snapshots/tick_1.json")));
  assert.ok(reports.some((l) => l.includes("not a record type: .cs): 250 files under harness/moores-code-review/runs/r1/refix/")));

  // 外した物が残っていても、後から足した報告書は載り、同じ物は再通知しない
  // Leftover rejected files neither block later reports nor get reported again.
  write("harness/moores-code-review/runs/r2/report.md", "# r2\n");
  assert.deepEqual(sync(), ["harness/moores-code-review/runs/r2/report.md"]);
  assert.equal(reports.length, 3);
});

test("frame sequences are caught even when written slowly across syncs", () => {
  const { write, sync, reports } = setup();
  for (let i = 0; i < 15; i++) write(`harness/playtest/r1/frames/f${i}.png`);
  assert.equal(sync().filter((p) => p.endsWith(".png")).length, 15);
  for (let i = 15; i < 30; i++) write(`harness/playtest/r1/frames/f${i}.png`);
  assert.deepEqual(sync(), []);
  assert.ok(reports.some((l) => l.includes("image sequence") && l.includes("15 files under harness/playtest/r1/frames/")));
});
