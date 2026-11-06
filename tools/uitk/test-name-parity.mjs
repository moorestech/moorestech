// Web のテスト名と NUnit の <web-test> と docs/uitk/disposition.md を突き合わせ、束ごとの差分を出す（依存なし）
// Matches Web test names, NUnit <web-test> tags and docs/uitk/disposition.md, reporting per-bundle gaps (no dependencies)
// 使い方: node tools/uitk/test-name-parity.mjs [--bundle 1-5|all] [--web-rev <rev>] [--emit-missing] [--root <repo>]
// Usage: node tools/uitk/test-name-parity.mjs [--bundle 1-5|all] [--web-rev <rev>] [--emit-missing] [--root <repo>]
// 終了コード: 0 = 差分 0、1 = 未対応か写す予定がある、2 = 書式・宛先・引数の誤り
// Exit codes: 0 = no gap, 1 = missing or pending tests remain, 2 = malformed input, dangling target or bad arguments
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";
import { BUNDLE_IDS, parseLedgerRows } from "./testNameParity/ledgerBundles.mjs";
import { parseDisposition } from "./testNameParity/disposition.mjs";
import { compareTests } from "./testNameParity/compareBundles.mjs";
import { readWebTestTags, readWebTests } from "./testNameParity/repositorySources.mjs";

const LEDGER_PATH = "docs/uitk/TODO.md";
const DISPOSITION_PATH = "docs/uitk/disposition.md";

// 想定外の失敗（git の失敗・読めないファイル）も「差分あり」の 1 と混ざらないよう 2 で落とす
// Unexpected failures (git errors, unreadable files) exit 2 so they never mix with the "gap remains" code 1
process.on("uncaughtException", (error) => {
  console.error(`突き合わせを実行できない: ${error.message}`);
  process.exit(2);
});

const { values } = parseArgs({
  options: {
    bundle: { type: "string", default: "all" },
    "web-rev": { type: "string" },
    "emit-missing": { type: "boolean", default: false },
    root: { type: "string" },
  },
});
const repositoryRoot = values.root ?? join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const selectedBundles = values.bundle === "all" ? BUNDLE_IDS : BUNDLE_IDS.filter((bundle) => String(bundle) === values.bundle);
if (selectedBundles.length === 0) {
  console.error(`--bundle は 1〜5 か all: ${values.bundle}`);
  process.exit(2);
}

// 台帳・処遇・Web テスト・NUnit のタグを読んで突き合わせる
// Read the ledger, dispositions, Web tests and NUnit tags, then match them
const ledger = parseLedgerRows(readFileSync(join(repositoryRoot, LEDGER_PATH), "utf8"));
const disposition = parseDisposition(readFileSync(join(repositoryRoot, DISPOSITION_PATH), "utf8"));
const webTests = readWebTests(repositoryRoot, values["web-rev"] ?? null);
const csTags = readWebTestTags(repositoryRoot);
const comparison = compareTests(webTests, csTags.tags, disposition.entries, ledger.rows);
const errors = [...ledger.errors, ...disposition.errors, ...csTags.errors, ...comparison.errors];

// 束ごとの件数と、未対応・写す予定の一覧を出す。0 件の束は走査か台帳の読み違いとして誤りにする
// Print per-bundle counts plus missing and pending lists; a bundle with zero tests is an error (scan or ledger misread)
let openCount = 0;
for (const bundle of selectedBundles) {
  const summary = comparison.bundles.get(bundle);
  if (summary.total === 0) errors.push(`束 ${bundle} の Web テストが 0 件（走査か台帳の読み違いの疑い）`);
  console.log(`束 ${bundle}: Web ${summary.total} 件 / 写した ${summary.copied} / 処遇 ${summary.disposed} / 写す予定 ${summary.pending.length} / 未対応 ${summary.missing.length}`);
  for (const test of summary.missing) console.log(`  未対応: ${test.file}:${test.line} ${test.title}`);
  for (const { test, entry } of summary.pending) console.log(`  写す予定: ${test.file}:${test.line} ${test.title}（${entry.evidence}）`);
  openCount += summary.missing.length + summary.pending.length;
}

// 未対応を処遇の表の行の形で出す。処遇の列が「要判断」のまま貼ると書式の誤り（2）で落ちる
// Emit missing tests as disposition rows; pasting them with "要判断" left in place fails as malformed (2)
if (values["emit-missing"]) {
  for (const bundle of selectedBundles) {
    for (const test of comparison.bundles.get(bundle).missing) {
      console.log(`| ${bundle} | \`${test.file}\` | ${test.title.replaceAll("|", "\\|")} | 要判断 | 要判断 | — |`);
    }
  }
}

if (errors.length > 0) {
  console.error(`構造の誤り ${errors.length} 件:`);
  for (const error of errors) console.error(`  ${error}`);
  process.exit(2);
}
process.exit(openCount > 0 ? 1 : 0);
