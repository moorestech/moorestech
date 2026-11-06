// Web UI のトークン CSS 3 枚を UITK の USS 3 枚と除外表へ機械変換する（依存なし）
// Mechanically converts the Web UI's 3 token CSS files into 3 UITK USS files plus an exclusion table (no dependencies)
// 使い方: リポジトリ直下で `node tools/uitk/convert-web-tokens.mjs`
// Usage: run `node tools/uitk/convert-web-tokens.mjs` from the repository root
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { parseRootTokens } from "./webTokens/parseCssTokens.mjs";
import { createTokenConverter } from "./webTokens/convertTokenValue.mjs";
import { darkExponent, translucencyMethod } from "./webTokens/translucency.mjs";
import { handWrittenDerivedRows } from "./webTokens/handWrittenDerivedRows.mjs";

const rampSourcePath = "moorestech_client/Assets/Scripts/Client.Ui/Components/Panels/GamePanel/GamePanelFaceOpacityRamp.cs";

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
const translucentTablePath = "docs/uitk/translucent-tokens.md";

// 見た目の例外（台帳 LX）に当たるトークン。生成する行に印を付け、LookExceptionLedgerTest が台帳と照合する
// Tokens that are look exceptions (ledger LX); the generated line carries the marker, which LookExceptionLedgerTest matches against the ledger
const lookExceptionMarkers = {
  "--tooltip-border-color": "LX-81",
  "--craft-arrow-outline": "LX-81",
  "--tutorial-attention-glow": "LX-81",
};

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
// 実機の撮影で決めたトークンの決め方。値を変えずに据え置いたものも理由を書く
// How tokens were settled on device; rows kept unchanged also state why
const measuredMethods = {
  "--surface-navy": `${translucencyMethod}。ランタイムの PanelSettings で地 30〜180 に載せて Web との差が最大 8（指数 2.2 では 16。I-fix1）。C2-a の持ち物・チャレンジ HUD の撮影で合う α は 0.944〜0.956`,
  "--tooltip-border-color": `${translucencyMethod}。ランタイムの PanelSettings で TooltipFace を地 (30,30,30) に描き、縁は UITK (71,72,75)・Web の式 (69,72,78)（α^2.2 の 0.0358 では暗い面の地で 17〜20 段暗かった。I-fix2）`,
  "--tutorial-attention-glow": `${translucencyMethod}。純色の赤は輝度では暗い色に入り 0.3981 で暗い地の上 87 段濃かった。最大成分で明るい色に分け、暗い地 16〜35 で差 5〜6・明るい地 120〜180 で 25〜37（LX-81。I-fix2）`,
};


writeOutput(exclusionFileName, exclusionLines);
writeTranslucentTable(converter.translucentRows);

function formatDeclaration(name, result) {
  const marker = lookExceptionMarkers[name] === undefined ? "" : ` /* look-exception: ${lookExceptionMarkers[name]} */`;
  const comment = result.comment === undefined ? "" : ` /* ${result.comment} */`;
  return `    ${name}: ${result.value};${marker}${comment}`;
}

function verifyHandWrittenRowsMatchDerivedTokens() {
  // 手書き行の UITK 値が DerivedTokens.uss の実値と違えば止める（片方だけ直して生成物が古い値を出すのを防ぐ）
  // Stop when a hand-written row's UITK value differs from DerivedTokens.uss, so a one-sided edit cannot leave stale generated values
  const uss = readFileSync(join(repositoryRoot, outputDirectory, "DerivedTokens.uss"), "utf8");
  for (const row of handWrittenDerivedRows) {
    const declaration = new RegExp(`${row.name}:\\s*([^;]+);`).exec(uss);
    if (declaration === null) throw new Error(`${row.name} is not declared in DerivedTokens.uss`);
    if (declaration[1].trim() !== row.uss) throw new Error(`${row.name}: hand-written row says '${row.uss}' but DerivedTokens.uss says '${declaration[1].trim()}'`);
  }
  // 縁フェードの逆算が使う C# 側の指数を生成器の指数と揃える（片方だけ直すと中間の濃さが黙ってずれる）
  // Keep the C# exponent used by the edge-fade inversion equal to the generator's (a one-sided edit would silently skew midtones)
  const ramp = readFileSync(join(repositoryRoot, rampSourcePath), "utf8");
  if (!ramp.includes(`LinearBlendExponent = ${darkExponent}f;`)) throw new Error(`${rampSourcePath} must declare LinearBlendExponent = ${darkExponent}f`);
}

function writeTranslucentTable(rows) {
  verifyHandWrittenRowsMatchDerivedTokens();
  const lines = [
    "<!-- Generated by node tools/uitk/convert-web-tokens.mjs — do not edit by hand. -->",
    "<!-- Regenerate: node tools/uitk/convert-web-tokens.mjs -->",
    "",
    "# 半透明トークン（Web の値と UITK の値）",
    "",
    "UITK は Linear 色空間で混ぜるため Web の α をそのまま使わない（plan.md §3.5・S7）。UITK の値は補正式の値で、実機で決め直したものは決め方の列に書く。色はランタイムの PanelSettings（Linear 合成）で比べる（部品ギャラリーは sRGB 合成で淡く出る）。",
    "",
    "| トークン | Web の値 | UITK の値 | 決め方 |",
    "| --- | --- | --- | --- |",
    ...rows.map((row) => `| \`${row.name}\` | \`${row.webValue}\` | \`${row.ussValue}\` | ${measuredMethods[row.name] ?? translucencyMethod} |`),
    ...handWrittenDerivedRows.map((row) => `| \`${row.name}\` | \`${row.web}\` | \`${row.uss}\` | ${row.method} |`),
  ];
  writeFileSync(join(repositoryRoot, translucentTablePath), `${lines.join("\n")}\n`);
  console.log(`wrote ${translucentTablePath} (${rows.length} rows)`);
}

function writeOutput(fileName, lines) {
  // 200 行を超える出力は規約違反なので書かずに止める
  // Output over 200 lines breaks the repository rule, so stop instead of writing it
  if (lines.length > maxLinesPerFile) throw new Error(`${fileName} would have ${lines.length} lines (max ${maxLinesPerFile})`);
  writeFileSync(join(repositoryRoot, outputDirectory, fileName), `${lines.join("\n")}\n`);
  console.log(`wrote ${outputDirectory}/${fileName} (${lines.length} lines)`);
}
