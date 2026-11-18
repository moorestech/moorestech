// Web テスト・NUnit のタグ・処遇の表を突き合わせ、束ごとに 写した／処遇／写す予定／未対応 を数える
// Matches Web tests, NUnit tags and disposition rows, counting copied / disposed / pending / missing per bundle
import { BUNDLE_IDS, dueBundle } from "./ledgerBundles.mjs";
import { VERDICT_PENDING, WHOLE_FILE } from "./disposition.mjs";

function keyOf(file, title) {
  return `${file}\u0000${title}`;
}

export function compareTests(webTests, tags, dispositions, ledgerRows) {
  const errors = [];

  // Web テストを (ファイル, 名前) で一意にする。同じファイルの同じ名前は 1 件に数える
  // Make Web tests unique by (file, title); a repeated title in one file counts once
  const webByKey = new Map();
  const webFiles = new Set();
  for (const test of webTests) {
    webFiles.add(test.file);
    if (!webByKey.has(keyOf(test.file, test.title))) webByKey.set(keyOf(test.file, test.title), test);
  }

  // タグの宛先が Web に実在するかを確かめる
  // Check that every tag points at an existing Web test
  const copiedKeys = new Set();
  for (const tag of tags) {
    const key = keyOf(tag.file, tag.title);
    if (!webByKey.has(key)) {
      errors.push(`${tag.csFile}:${tag.line}: <web-test> の宛先が Web に無い: ${tag.file} :: ${tag.title}`);
      continue;
    }
    copiedKeys.add(key);
  }
  const { byTitle, byFile } = indexDispositions(dispositions, { webByKey, webFiles, copiedKeys, ledgerRows }, errors);

  // 束ごとに分類する。タグが処遇の * より優先する
  // Classify per bundle; a tag takes precedence over a whole-file * row
  const bundles = new Map(BUNDLE_IDS.map((bundle) => [bundle, { total: 0, copied: 0, disposed: 0, pending: [], missing: [] }]));
  const unownedFiles = new Set();
  for (const [key, test] of webByKey) {
    const due = dueBundle(test.file, ledgerRows);
    if (due === null) {
      if (!unownedFiles.has(test.file)) errors.push(`台帳のどの行にも入っていない Web テストのファイル: ${test.file}`);
      unownedFiles.add(test.file);
      continue;
    }
    const summary = bundles.get(due);
    summary.total++;
    if (copiedKeys.has(key)) {
      summary.copied++;
      continue;
    }
    const entry = byTitle.get(key) ?? byFile.get(test.file);
    if (entry === undefined) summary.missing.push(test);
    else if (entry.verdict === VERDICT_PENDING) summary.pending.push({ test, entry });
    else summary.disposed++;
  }
  return { bundles, errors };
}

function indexDispositions(dispositions, context, errors) {
  // 処遇の行の宛先・束・重複・タグとの矛盾を確かめて索引にする
  // Validate each disposition row's target, bundle, duplicates and conflicts with tags, then index it
  const { webByKey, webFiles, copiedKeys, ledgerRows } = context;
  const byTitle = new Map();
  const byFile = new Map();
  for (const entry of dispositions) {
    const where = `disposition ${entry.lineNumber} 行目`;
    if (!webFiles.has(entry.file)) {
      errors.push(`${where}: Web のテストファイルが無い: ${entry.file}`);
      continue;
    }
    const due = dueBundle(entry.file, ledgerRows);
    if (due !== entry.bundle) {
      errors.push(`${where}: 束 ${entry.bundle} と書いたが台帳では束 ${due}`);
      continue;
    }
    if (entry.title === WHOLE_FILE) {
      if (byFile.has(entry.file)) errors.push(`${where}: ${entry.file} の * が重複している`);
      else byFile.set(entry.file, entry);
      continue;
    }

    // 名前を書いた行は、実在・重複なし・タグと両立しないことを確かめる
    // A titled row must exist, be unique and not coexist with a tag
    const key = keyOf(entry.file, entry.title);
    if (!webByKey.has(key)) errors.push(`${where}: Web のテストが無い: ${entry.file} :: ${entry.title}`);
    else if (byTitle.has(key)) errors.push(`${where}: 同じテストの行が重複している`);
    else if (copiedKeys.has(key)) errors.push(`${where}: <web-test> で写したテストに処遇の行もある`);
    else byTitle.set(key, entry);
  }
  return { byTitle, byFile };
}
