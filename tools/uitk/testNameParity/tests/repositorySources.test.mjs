// repositorySources.mjs の自己テスト: 未追跡の新規 .cs の <web-test> も数える
// Self-test for repositorySources.mjs: an untracked new .cs still has its <web-test> counted
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdirSync, mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { readWebTestTags, readWebTests } from "../repositorySources.mjs";

test("git add していない新規 .cs の <web-test> を集める", () => {
  const root = mkdtempSync(join(tmpdir(), "repository-sources-"));
  execFileSync("git", ["init", "-q"], { cwd: root });
  const path = "moorestech_client/Assets/Scripts/Client.Tests/NewTest.cs";
  mkdirSync(dirname(join(root, path)), { recursive: true });
  writeFileSync(join(root, path), '/// <web-test file="src/a.test.ts">名前</web-test>\n[Test] public void T() {}\n');
  const { tags, errors } = readWebTestTags(root);
  assert.deepEqual(errors, []);
  assert.equal(tags.length, 1);
  assert.equal(tags[0].csFile, path);
});

test("非 ASCII 名の .cs の <web-test> と Web テストを読む", () => {
  // git が引用・8 進エスケープしないよう -z で読めていることを確かめる
  // Verify paths are read with -z so git does not quote or octal-escape them
  const root = mkdtempSync(join(tmpdir(), "repository-sources-"));
  execFileSync("git", ["init", "-q"], { cwd: root });
  const csPath = "moorestech_client/Assets/Scripts/Client.Tests/日本語Test.cs";
  const webPath = "moorestech_web/webui/src/日本語.test.ts";
  for (const path of [csPath, webPath]) mkdirSync(dirname(join(root, path)), { recursive: true });
  writeFileSync(join(root, csPath), '/// <web-test file="src/a.test.ts">名前</web-test>\n[Test] public void T() {}\n');
  writeFileSync(join(root, webPath), 'it("名前", () => {});\n');
  const { tags } = readWebTestTags(root);
  assert.equal(tags.length, 1);
  assert.equal(tags[0].csFile, csPath);
  const webTests = readWebTests(root, null);
  assert.equal(webTests.length, 1);
  assert.equal(webTests[0].file, "src/日本語.test.ts");
});
