// docs/uitk/disposition.md の「## 処遇」の表を読み、行ごとに書式を検査する（依存なし）
// Reads the "## 処遇" table of docs/uitk/disposition.md and validates each row (no dependencies)
import { readSectionTable } from "./markdownTable.mjs";
import { normalizeTitle } from "./webTests.mjs";
import { isBundleId } from "./ledgerBundles.mjs";

export const DISPOSITION_HEADING = "## 処遇";
export const DISPOSITION_HEADER = ["束", "Web のファイル", "Web のテスト名", "処遇", "証跡のパスか理由", "PR"];
export const VERDICT_VISUAL = "目視";
export const VERDICT_RECORDING = "録画";
export const VERDICT_DISCARD = "捨てる";
export const VERDICT_PENDING = "写す予定";
export const WHOLE_FILE = "*";
const VERDICTS = [VERDICT_VISUAL, VERDICT_RECORDING, VERDICT_DISCARD, VERDICT_PENDING];
const EMPTY_CELLS = ["", "—"];
// 目視の証跡: 台帳「参照キャプチャ」列の撮影 id と、UITK 側のスクショ
// Visual-check evidence: a capture id from the ledger "参照キャプチャ" column and a UITK screenshot
const CAPTURE_ID = /(?<![\w-])[1-5]-[a-z][a-z0-9-]*/;
const SCREENSHOT = /\.(png|jpe?g|webp)(?![\w])/;

export function parseDisposition(text) {
  const { rows, errors } = readSectionTable(text, DISPOSITION_HEADING, DISPOSITION_HEADER);
  const entries = [];
  for (const { cells, lineNumber } of rows) {
    const [rawBundle, rawFile, rawTitle, verdict, evidence] = cells;
    const bundle = Number(rawBundle);
    const fileMatch = /^`([^`]+)`$/.exec(rawFile);
    const title = normalizeTitle(rawTitle);

    // 行の誤りを全部集めてから 1 行にまとめて返す
    // Collect every problem of the row, then report them on one line
    const problems = [];
    if (!isBundleId(bundle)) problems.push(`束「${rawBundle}」が 1〜5 ではない`);
    if (fileMatch === null) problems.push(`Web のファイル「${rawFile}」が \`…\` で囲まれていない`);
    if (title === "") problems.push("Web のテスト名が空");
    if (!VERDICTS.includes(verdict)) problems.push(`処遇「${verdict}」が ${VERDICTS.join("・")} のどれでもない`);
    if (EMPTY_CELLS.includes(evidence)) problems.push("証跡のパスか理由が空");
    if (verdict === VERDICT_RECORDING && !/scenarios\/.+\.cs/.test(evidence)) problems.push("録画は証跡にシナリオのパス（scenarios/…/*.cs）を書く");
    if (verdict === VERDICT_VISUAL && !(CAPTURE_ID.test(evidence) && SCREENSHOT.test(evidence))) problems.push("目視は証跡に参照キャプチャの撮影 id（1-toast の形）と UITK 側のスクショのパスか URL（.png・.jpg）を書く");
    if (verdict === VERDICT_PENDING && !/moorestech-[0-9a-z.]+/.test(evidence)) problems.push("写す予定は証跡に bd の ID（moorestech-…）を書く");
    if (problems.length > 0) {
      errors.push(`disposition ${lineNumber} 行目: ${problems.join("／")}`);
      continue;
    }
    entries.push({ bundle, file: fileMatch[1], title, verdict, evidence, lineNumber });
  }
  return { entries, errors };
}
