// Web UI のトークン CSS 3 枚を UITK の USS 3 枚と除外表へ機械変換する（依存なし）
// Mechanically converts the Web UI's 3 token CSS files into 3 UITK USS files plus an exclusion table (no dependencies)
// 使い方: リポジトリ直下で `node tools/uitk/convert-web-tokens.mjs`
// Usage: run `node tools/uitk/convert-web-tokens.mjs` from the repository root
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { parseRootTokens } from "./webTokens/parseCssTokens.mjs";
import { createTokenConverter } from "./webTokens/convertTokenValue.mjs";
import { translucencyMethod } from "./webTokens/translucency.mjs";

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
// ControlTokens.uss は手書きで変換器を通らないため、半透明の行をここで足す（Web の値は PoC と GamePanel/style.module.css のもの）
// ControlTokens.uss is hand-written and bypasses the converter, so its translucent rows are added here (Web values from the PoC and GamePanel/style.module.css)
const handWrittenControlRows = [
  `| \`--control-scroller-track\` | \`rgba(255, 255, 255, 0.04)\` | \`rgba(255, 255, 255, 0.0008)\` | ${translucencyMethod} |`,
  `| \`--game-panel-title-shadow\` | \`rgba(0, 0, 0, 0.35)\` | \`rgba(0, 0, 0, 0.3735)\` | 面色を背景にした厳密式（S7） |`,
  `| \`--game-panel-rule-top-start\` | \`rgba(146, 148, 167, 0.42)\` | \`rgba(146, 148, 167, 0.1961)\` | 面色を背景にした厳密式（S7） |`,
  `| \`--game-panel-rule-top-peak\` | \`rgba(146, 148, 167, 0.59)\` | \`rgba(146, 148, 167, 0.3602)\` | 面色を背景にした厳密式（S7） |`,
  `| \`--game-panel-rule-top-end\` | \`rgba(146, 148, 167, 0.38)\` | \`rgba(146, 148, 167, 0.1653)\` | 面色を背景にした厳密式（S7） |`,
  `| \`--game-panel-rule-bottom-start\` | \`rgba(128, 132, 159, 0.3)\` | \`rgba(128, 132, 159, 0.1178)\` | 面色を背景にした厳密式（S7） |`,
  `| \`--game-panel-rule-bottom-peak\` | \`rgba(128, 132, 159, 0.42)\` | \`rgba(128, 132, 159, 0.2034)\` | 面色を背景にした厳密式（S7） |`,
  `| \`--game-panel-rule-bottom-end\` | \`rgba(128, 132, 159, 0.26)\` | \`rgba(128, 132, 159, 0.0946)\` | 面色を背景にした厳密式（S7） |`,
  `| \`--game-panel-triangle-face\` | \`rgba(94, 100, 128, 0.2)\` | \`rgba(94, 100, 128, 0.0764)\` | 面色を背景にした厳密式（S7） |`,
  `| \`--panel-action-hover-face\` | \`color-mix(in srgb, var(--gauge-track), var(--bevel-c2) 22%)\` = \`rgba(35, 38, 51, 0.844)\` | \`rgba(35, 38, 51, 0.9832)\` | 実機で据え置き（ポーズの撮影でホバー中のボタンの面が地の面と見分けられる） |`,
  `| \`--mode-switch-selected-face\` | \`color-mix(in srgb, var(--gauge-track), var(--bevel-c2) 30%)\` = \`rgba(43, 46, 59, 0.86)\` | \`rgba(43, 46, 59, 0.9868)\` | ${translucencyMethod} |`,
  `| \`--panel-action-text-outline\` | \`rgb(0 0 0 / 70%)\` | \`rgba(0, 0, 0, 0.9293)\` | ${translucencyMethod} |`,
];

// 実機の撮影で決めたトークンの決め方。値を変えずに据え置いたものも理由を書く
// How tokens were settled on device; rows kept unchanged also state why
const measuredMethods = {
  "--surface-navy": "実機で据え置き（ポーズの GamePanel を PlayMode で撮影）。暗い背景で背景の透けは実効 α 0.95〜0.99 と Web の 0.8 より濃いが、題・ボタンの文字は読める。明るい地表で Web に合うのは約 0.944 だが、ゲージ・通知・ワールドピンと共有の面なので変えない",
};

writeOutput(exclusionFileName, exclusionLines);
writeTranslucentTable(converter.translucentRows);

function formatDeclaration(name, result) {
  const comment = result.comment === undefined ? "" : ` /* ${result.comment} */`;
  return `    ${name}: ${result.value};${comment}`;
}

function writeTranslucentTable(rows) {
  const lines = [
    "<!-- Generated by node tools/uitk/convert-web-tokens.mjs — do not edit by hand. -->",
    "<!-- Regenerate: node tools/uitk/convert-web-tokens.mjs -->",
    "",
    "# 半透明トークン（Web の値と UITK の値）",
    "",
    "UITK は Linear 色空間で混ぜるため Web の α をそのまま使わない（plan.md §3.5・S7）。UITK の値は補正式の初期値で、部品を作る段で実機で決め直し、その時は決め方の列を書き換える。",
    "",
    "| トークン | Web の値 | UITK の値 | 決め方 |",
    "| --- | --- | --- | --- |",
    ...rows.map((row) => `| \`${row.name}\` | \`${row.webValue}\` | \`${row.ussValue}\` | ${measuredMethods[row.name] ?? translucencyMethod} |`),
    ...handWrittenControlRows,
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
