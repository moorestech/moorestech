// nunitWebTags.mjs の自己テスト: <web-test> の抽出と書式の誤り
// Self-test for nunitWebTags.mjs: <web-test> extraction and malformed tags
import { test } from "node:test";
import assert from "node:assert/strict";
import { extractWebTestTags } from "../nunitWebTags.mjs";

test("/// <web-test file=…> の宛先と名前を XML の実体参照を戻して拾う", () => {
  const text = [
    '        /// <web-test file="src/features/toast/toastStore.test.ts">emitToast で追加され 3秒後に消える</web-test>',
    '        /// <web-test file="src/a.test.ts">a &lt; b &amp;&amp; c</web-test>',
    "        [Test]",
  ].join("\n");
  assert.deepEqual(extractWebTestTags(text), {
    tags: [
      { file: "src/features/toast/toastStore.test.ts", title: "emitToast で追加され 3秒後に消える", line: 1 },
      { file: "src/a.test.ts", title: "a < b && c", line: 2 },
    ],
    errors: [],
  });
});

test("閉じタグの無い行や属性名の違う行は書式の誤りとして返す", () => {
  const text = ['/// <web-test file="src/a.test.ts">閉じていない', '/// <web-test path="src/a.test.ts">x</web-test>'].join("\n");
  const result = extractWebTestTags(text);
  assert.equal(result.tags.length, 0);
  assert.deepEqual(result.errors.map((error) => error.line), [1, 2]);
});
