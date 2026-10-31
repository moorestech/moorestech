// 削った記録が元に戻ること、戻せない内容は削らないことを確かめる
// Trimmed transcripts restore exactly, and unrestorable content is never trimmed.

import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";
import { createTrimContext, writeTrimmedTranscript } from "../trim-file.mjs";
import { commitFile, createRepos, longText, readRoundTrip } from "./fixture.mjs";

const RESTORE = fileURLToPath(new URL("../restore-transcript.mjs", import.meta.url));

function writeJsonl(path, lines) {
  writeFileSync(path, lines.map((l) => JSON.stringify(l)).join("\n") + "\n");
}

function restore(file, ...args) {
  return execFileSync("node", [RESTORE, file, ...args], { encoding: "utf8" });
}

test("committed reads and boilerplate are trimmed and restore exactly, even after the worktree is gone", () => {
  const { root, clone, logs } = createRepos();
  const committed = commitFile(clone, "docs/plan.md", longText("plan"));
  const lines = [
    ...readRoundTrip("t1", committed, longText("plan").split("\n").slice(9, 109).join("\n"), 10),
    { type: "attachment", attachment: { type: "prompt_snapshot", systemPrompt: [longText("system", 300)] } },
  ];
  const src = join(root, "agent.jsonl");
  const dest = join(logs, "claude", "slug", "s1", "subagents", "agent.jsonl");
  writeJsonl(src, lines);

  const trimmed = writeTrimmedTranscript(src, dest, "claude", createTrimContext(logs));

  assert.equal(trimmed, 2);
  const stored = readFileSync(dest, "utf8");
  assert.ok(stored.length < readFileSync(src, "utf8").length / 4);
  assert.match(stored, /"\$trim":"git"/);
  assert.match(stored, /"\$trim":"blob"/);
  // worktree が片付けられても、同じ origin の clone から戻せる
  // Restoration still works from a clone with the same origin after the recorded worktree is removed.
  const moved = join(root, "elsewhere");
  execFileSync("mv", [clone, moved]);
  assert.equal(restore(dest, "--repo", moved), readFileSync(src, "utf8"));
  // 記録した場所に git 管理外のディレクトリが置かれていても、clone へ切り替える
  // A non-repo directory now sitting at the recorded path does not block falling back to the clone.
  mkdirSync(clone);
  assert.equal(restore(dest, "--repo", moved), readFileSync(src, "utf8"));
});

test("reads of uncommitted or unpushed content keep their numbered display and restore exactly", () => {
  const { root, clone, logs } = createRepos();
  const unpushed = commitFile(clone, "src/new.cs", longText("new"), { push: false });
  const scratch = join(root, "scratch.txt");
  writeFileSync(scratch, longText("scratch"));
  const lines = [...readRoundTrip("a", unpushed, longText("new")), ...readRoundTrip("b", scratch, longText("scratch"))];
  const src = join(root, "agent.jsonl");
  const dest = join(logs, "claude", "slug", "s2", "subagents", "agent.jsonl");
  writeJsonl(src, lines);

  assert.equal(writeTrimmedTranscript(src, dest, "claude", createTrimContext(logs)), 2);
  const stored = readFileSync(dest, "utf8");
  // 中身はコミットに頼れないので行番号つき表示に残り、生テキストの二重記録だけが外れる
  // The content stays in the numbered display (no commit to lean on); only the duplicate raw copy is removed.
  assert.doesNotMatch(stored, /"\$trim":"(git|logs)"/);
  assert.match(stored, /1\\tnew line 0/);
  assert.equal(restore(dest), readFileSync(src, "utf8"));
});

test("images read by several agents are stored once and restore exactly", () => {
  const { root, logs } = createRepos();
  const data = Buffer.from(longText("png bytes")).toString("base64");
  const imageRead = (id) => [
    { type: "assistant", message: { content: [{ type: "tool_use", id, name: "Read", input: { file_path: "/tmp/shot.png" } }] } },
    {
      type: "user",
      message: { content: [{ type: "tool_result", tool_use_id: id, content: [{ type: "image", source: { type: "base64", media_type: "image/png", data } }] }] },
      toolUseResult: { type: "image", file: { base64: data, type: "image/png" } },
    },
  ];
  const ctx = createTrimContext(logs);
  for (const name of ["a", "b"]) {
    const src = join(root, `${name}.jsonl`);
    const dest = join(logs, "claude", "slug", "s4", "subagents", `${name}.jsonl`);
    writeJsonl(src, imageRead(name));
    assert.equal(writeTrimmedTranscript(src, dest, "claude", ctx), 1);
    assert.doesNotMatch(readFileSync(dest, "utf8"), new RegExp(data.slice(0, 40)));
    assert.equal(restore(dest), readFileSync(src, "utf8"));
  }
  assert.equal(execFileSync("find", [join(logs, "blobs"), "-type", "f"], { encoding: "utf8" }).trim().split("\n").length, 1);
});

test("a truncated blob is rewritten on the next trim and refused on restore", () => {
  const { root, logs } = createRepos();
  const system = longText("system", 300);
  const src = join(root, "agent.jsonl");
  const dest = join(logs, "claude", "slug", "s5", "subagents", "agent.jsonl");
  writeJsonl(src, [{ type: "attachment", attachment: { type: "prompt_snapshot", systemPrompt: [system] } }]);
  writeTrimmedTranscript(src, dest, "claude", createTrimContext(logs));
  const blob = execFileSync("find", [join(logs, "blobs"), "-type", "f"], { encoding: "utf8" }).trim();

  writeFileSync(blob, system.slice(0, 100));
  assert.throws(() => restore(dest), /blob content does not match/);
  writeTrimmedTranscript(src, dest, "claude", createTrimContext(logs));
  assert.equal(readFileSync(blob, "utf8"), system);
  assert.equal(restore(dest), readFileSync(src, "utf8"));
});

test("reads of another subagent transcript are not pointed at, since it gets rewritten", () => {
  const { root, logs } = createRepos();
  const home = join(root, "home");
  process.env.HOME = home;
  const rel = join("slug", "s6", "subagents", "agent-b.jsonl");
  const body = longText("other agent");
  for (const base of [join(home, ".claude", "projects"), join(logs, "claude")]) {
    mkdirSync(join(base, "slug", "s6", "subagents"), { recursive: true });
    writeFileSync(join(base, rel), body);
  }
  const src = join(root, "agent.jsonl");
  const dest = join(logs, "claude", "slug", "s6", "subagents", "agent-z.jsonl");
  writeJsonl(src, readRoundTrip("z", join(home, ".claude", "projects", rel), body));
  writeTrimmedTranscript(src, dest, "claude", createTrimContext(logs));
  assert.doesNotMatch(readFileSync(dest, "utf8"), /"\$trim":"logs"/);
});

test("re-reads of a mirrored tool-results file point into the logs repo", () => {
  const { logs, root } = createRepos();
  const home = join(root, "home");
  process.env.HOME = home;
  const rel = join("slug", "s3", "tool-results", "out.txt");
  const body = longText("tool output");
  for (const base of [join(home, ".claude", "projects"), join(logs, "claude")]) {
    mkdirSync(join(base, "slug", "s3", "tool-results"), { recursive: true });
    writeFileSync(join(base, rel), body);
  }
  const src = join(root, "agent.jsonl");
  const dest = join(logs, "claude", "slug", "s3", "subagents", "agent.jsonl");
  writeJsonl(src, readRoundTrip("c", join(home, ".claude", "projects", rel), body));

  assert.equal(writeTrimmedTranscript(src, dest, "claude", createTrimContext(logs)), 1);
  assert.match(readFileSync(dest, "utf8"), /"\$trim":"logs"/);
  assert.equal(restore(dest), readFileSync(src, "utf8"));
  rmSync(join(logs, "claude", rel));
  assert.throws(() => restore(dest), /restore failed at line 2/);
});
