// C# のテストに書いた /// <web-test file="…">Web のテスト名</web-test> を拾う（依存なし）
// Extracts /// <web-test file="…">Web test name</web-test> written on C# tests (no dependencies)
import { normalizeTitle } from "./webTests.mjs";

const TAG_LINE = /^\s*\/\/\/\s*<web-test file="([^"]+)">(.*)<\/web-test>\s*$/;

export function extractWebTestTags(text) {
  const tags = [];
  const errors = [];
  text.split(/\r?\n/).forEach((line, index) => {
    if (!line.includes("<web-test")) return;

    // 1 行 1 タグの形に合わない行は、写した印として数えず書式の誤りにする
    // A line not in the one-tag-per-line form is not counted as copied; it is reported as malformed
    const match = TAG_LINE.exec(line);
    if (match === null) {
      errors.push({ line: index + 1, text: line.trim() });
      return;
    }
    tags.push({ file: match[1], title: normalizeTitle(decodeXml(match[2])), line: index + 1 });
  });
  return { tags, errors };
}

function decodeXml(text) {
  // &amp; は最後に戻す（&amp;lt; を < にしないため）
  // Decode &amp; last so &amp;lt; does not become <
  return text
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, "&");
}
