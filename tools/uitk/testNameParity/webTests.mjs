// Web（vitest・Playwright）のテストファイルからテスト名を拾う（依存なし。AST ではなく正規表現で読む）
// Extracts test names from Web (vitest / Playwright) test files (no dependencies; regex-based, no AST)

// moorestech_web/webui/ からの相対で、src と e2e の下の *.test.ts(x)・*.spec.ts(x)
// Relative to moorestech_web/webui/: *.test.ts(x) and *.spec.ts(x) under src and e2e
const WEB_TEST_FILE = /^(src|e2e)\/.*\.(test|spec)\.tsx?$/;

// it/test（only・skip などの修飾と .each(...) を許す）の第 1 引数の文字列。describe・step・beforeEach は拾わない
// The first string argument of it/test (modifiers and .each(...) allowed); describe, step and beforeEach are not matched
const TEST_CALL =
  /(?<![\w$.])(?:it|test)(?:\.(?:only|skip|concurrent|fails|sequential))*(?:\.each\s*(?:<[^()]*?>\s*)?(?:`[^`]*`|\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\)))?\s*\(\s*(?:(['"])((?:\\.|(?!\1)[^\\\n])*)\1|`((?:\\[\s\S]|[^\\`])*)`)\s*,/g;

export function isWebTestFile(webPath) {
  return WEB_TEST_FILE.test(webPath);
}

export function normalizeTitle(title) {
  // 空白の連続と改行を 1 つの空白に畳む（C# の 1 行コメントと表のセルに収まる形にする）
  // Collapse whitespace runs and newlines into one space (fits a one-line C# comment and a table cell)
  return title.replace(/\s+/g, " ").trim();
}

export function extractWebTestTitles(text) {
  const titles = [];
  for (const match of text.matchAll(TEST_CALL)) {
    // 引用符のエスケープだけを戻す。${…} は展開せずそのまま名前にする
    // Undo quote escapes only; ${…} stays unexpanded in the name
    const literal = match[2] ?? match[3];
    const line = text.slice(0, match.index).split("\n").length;
    titles.push({ title: normalizeTitle(literal.replace(/\\(['"`\\])/g, "$1")), line });
  }
  return titles;
}
