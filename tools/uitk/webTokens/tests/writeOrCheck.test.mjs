import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, writeFileSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { writeOrCheck } from "../writeOrCheck.mjs";

test("check は同じ内容なら書かず exit 0 のまま", () => {
  const root = mkdtempSync(join(tmpdir(), "tokens-"));
  writeFileSync(join(root, "a.uss"), "same\n");
  process.exitCode = 0;
  writeOrCheck(root, "a.uss", "same\n", true);
  assert.equal(process.exitCode, 0);
});

test("check は違えば書かず exit 1", () => {
  const root = mkdtempSync(join(tmpdir(), "tokens-"));
  writeFileSync(join(root, "a.uss"), "old\n");
  process.exitCode = 0;
  writeOrCheck(root, "a.uss", "new\n", true);
  assert.equal(process.exitCode, 1);
  assert.equal(readFileSync(join(root, "a.uss"), "utf8"), "old\n");
  process.exitCode = 0;
});
