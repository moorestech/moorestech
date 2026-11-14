// 生成物を書くか、--check のときは書かずに今のファイルと比べる（手で変えた・変換し忘れたトークンを CI で止める。DC-30）
// Write a generated file, or with --check compare it against the current file without writing (CI stops hand-edited or unconverted tokens; DC-30)
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

export function writeOrCheck(repositoryRoot, relativePath, text, checkMode) {
  const absolutePath = join(repositoryRoot, relativePath);
  if (!checkMode) {
    writeFileSync(absolutePath, text);
    console.log(`wrote ${relativePath}`);
    return;
  }
  const current = existsSync(absolutePath) ? readFileSync(absolutePath, "utf8") : null;
  if (current === text) {
    console.log(`up to date ${relativePath}`);
    return;
  }
  console.error(`${relativePath} differs from the converter output; run the converter without --check and commit the result`);
  process.exitCode = 1;
}
