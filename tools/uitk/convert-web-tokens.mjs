// Web UI のトークン CSS 3 枚を UITK の USS 3 枚と除外表へ機械変換する（依存なし）
// Mechanically converts the Web UI's 3 token CSS files into 3 UITK USS files plus an exclusion table (no dependencies)
// 使い方: リポジトリ直下で `node tools/uitk/convert-web-tokens.mjs`
// Usage: run `node tools/uitk/convert-web-tokens.mjs` from the repository root
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { parseRootTokens } from "./webTokens/parseCssTokens.mjs";
import { createTokenConverter } from "./webTokens/convertTokenValue.mjs";

const repositoryRoot = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const sourceDirectory = "moorestech_web/webui/src/app";
const outputDirectory = "moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme";
const conversions = [
  { source: "tokens.css", output: "Tokens.uss" },
  { source: "skitTokens.css", output: "SkitTokens.uss" },
  { source: "slotTokens.css", output: "SlotTokens.uss" },
];
const exclusionFileName = "TokenConversionExclusions.txt";
const maxLinesPerFile = 200;

// 見た目の例外（台帳 LX）に当たるトークン。生成する行に印を付け、LookExceptionLedgerTest が台帳と照合する
// Tokens that are look exceptions (ledger LX); the generated line carries the marker, which LookExceptionLedgerTest matches against the ledger
const lookExceptionMarkers = {};

// 全ファイルのトークンを先に集め、ファイルを跨ぐ参照も解決できるようにする
// Collect every file's tokens first so references across files resolve
const tokensBySource = conversions.map((conversion) => {
  const text = readFileSync(join(repositoryRoot, sourceDirectory, conversion.source), "utf8");
  return { ...conversion, tokens: parseRootTokens(text) };
});
const rawByName = new Map();
for (const { source, tokens } of tokensBySource) {
  for (const token of tokens) {
    if (rawByName.has(token.name)) throw new Error(`${token.name} is declared twice (last seen in ${source})`);
    rawByName.set(token.name, token.raw);
  }
}

const converter = createTokenConverter(rawByName);
const exclusionLines = [];
for (const { source, output, tokens } of tokensBySource) {
  const lines = [
    `/* Generated from ${sourceDirectory}/${source} — do not edit by hand. */`,
    "/* Regenerate: node tools/uitk/convert-web-tokens.mjs */",
    ":root {",
  ];
  for (const token of tokens) {
    const result = converter.convert(token.name);
    if (result.excluded) {
      exclusionLines.push(`${token.name}\t${result.reason}`);
      continue;
    }
    // 分割したトークンは派生名で書き、元の名前は除外表に「分割: …」で残す
    // A split token is written under its derived names, and the original stays in the exclusion table as "分割: …"
    if (result.derived !== undefined) {
      exclusionLines.push(`${token.name}\t分割: ${result.derived.map((derived) => derived.name).join(", ")}`);
      for (const derived of result.derived) lines.push(formatDeclaration(derived.name, derived));
      continue;
    }
    lines.push(formatDeclaration(token.name, result));
  }
  lines.push("}");
  writeOutput(output, lines);
}

writeOutput(exclusionFileName, exclusionLines);

function formatDeclaration(name, result) {
  const marker = lookExceptionMarkers[name] === undefined ? "" : ` /* look-exception: ${lookExceptionMarkers[name]} */`;
  const comment = result.comment === undefined ? "" : ` /* ${result.comment} */`;
  return `    ${name}: ${result.value};${marker}${comment}`;
}

function writeOutput(fileName, lines) {
  // 200 行を超える出力は規約違反なので書かずに止める
  // Output over 200 lines breaks the repository rule, so stop instead of writing it
  if (lines.length > maxLinesPerFile) throw new Error(`${fileName} would have ${lines.length} lines (max ${maxLinesPerFile})`);
  writeFileSync(join(repositoryRoot, outputDirectory, fileName), `${lines.join("\n")}\n`);
  console.log(`wrote ${outputDirectory}/${fileName} (${lines.length} lines)`);
}
