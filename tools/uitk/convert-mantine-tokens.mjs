// Mantine の暗色テーマが解決する --mantine-* と部品の寸法の変数（Button・Input）の実値を MantineTokens.uss へ書き出す（設計書 §3.1・C4）
// Writes the values Mantine's dark theme resolves for --mantine-* and the component size variables (Button, Input) into MantineTokens.uss (design §3.1, C4)
// 使い方: リポジトリ直下で `pnpm --dir moorestech_web/webui install --frozen-lockfile && node tools/uitk/convert-mantine-tokens.mjs`
// Usage: from the repository root, `pnpm --dir moorestech_web/webui install --frozen-lockfile && node tools/uitk/convert-mantine-tokens.mjs`
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { createTokenConverter } from "./webTokens/convertTokenValue.mjs";
import { writeOrCheck } from "./webTokens/writeOrCheck.mjs";
import { mantineTokenNames, readMantineRawTokens, resolveVarChain } from "./webTokens/mantineTokens.mjs";

const repositoryRoot = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const mantineCssPath = "moorestech_web/webui/node_modules/@mantine/core/styles.css";
const mantinePackagePath = "moorestech_web/webui/node_modules/@mantine/core/package.json";
const providerPath = "moorestech_web/webui/src/main.tsx";
const outputPath = "moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/MantineTokens.uss";
const maxLinesPerFile = 200;

// Web が theme を上書きしていれば styles.css の既定値は Web の実値ではないので止める
// Stop when the Web overrides the theme, since styles.css defaults would not be the Web's real values
const providerSource = readFileSync(join(repositoryRoot, providerPath), "utf8");
if (!providerSource.includes('<MantineProvider defaultColorScheme="dark">')) throw new Error(`${providerPath}: MantineProvider is not the plain defaultColorScheme="dark" form; theme overrides must be applied before generating`);

const version = JSON.parse(readFileSync(join(repositoryRoot, mantinePackagePath), "utf8")).version;
const raw = readMantineRawTokens(readFileSync(join(repositoryRoot, mantineCssPath), "utf8"));

// 出す名前は var の鎖を生の値まで辿ってから変換器に渡す（calc・rem・色の計算は既存の変換器に任せる）
// Emitted names follow their var chains to raw values before conversion (calc, rem and color math are left to the existing converter)
const resolved = new Map(raw);
for (const name of mantineTokenNames()) resolved.set(name, resolveVarChain(raw, name));
const converter = createTokenConverter(resolved);

const lines = [
  `/* Generated from ${mantineCssPath} (@mantine/core ${version}, defaultColorScheme dark) — do not edit by hand. */`,
  "/* Regenerate: pnpm --dir moorestech_web/webui install --frozen-lockfile && node tools/uitk/convert-mantine-tokens.mjs */",
  ":root {",
];
for (const name of mantineTokenNames()) {
  const result = converter.convert(name);
  if (result.excluded || result.derived !== undefined) throw new Error(`${name}: cannot convert '${resolved.get(name)}' (${result.reason ?? "split"})`);
  if (result.value.startsWith("var(")) throw new Error(`${name}: still a reference after resolving (${result.value})`);
  lines.push(`    ${name}: ${result.value}; /* web: ${raw.get(name)} */`);
}
lines.push("}");

if (lines.length > maxLinesPerFile) throw new Error(`${outputPath} would have ${lines.length} lines (max ${maxLinesPerFile})`);
writeOrCheck(repositoryRoot, outputPath, `${lines.join("\n")}\n`, process.argv.includes("--check"));
