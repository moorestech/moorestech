// 1 トークンの CSS 値を USS の値へ変換するか、除外理由を返す。参照先は全ファイル横断で解決する
// Converts one token's CSS value to a USS value or returns an exclusion reason; references resolve across all files
import { evaluateCalc, numberWithUnit } from "./evaluateCalc.mjs";
import { formatColor, parseColor } from "./cssColors.mjs";
import { formatNumber } from "./parseCssTokens.mjs";
import { convertCompound } from "./compoundTokens.mjs";

const viewportReason = "実行時の寸法: USS の配置か C# の幾何";
const viewportNamePattern = /^--ui-(stage|viewport)-/;

export function createTokenConverter(rawByName) {
  const baseResults = new Map();
  const finalResults = new Map();
  return { convert };

  // 出力用。半透明の α は Web の値のまま（UI は Web と同じ sRGB 空間で合成する。A-180）
  // For output; translucent alpha stays the Web value (the UI blends in sRGB space like the Web, A-180)
  function convert(name) {
    if (!finalResults.has(name)) finalResults.set(name, finalize(name, convertBase(name)));
    return finalResults.get(name);
  }

  function finalize(name, base) {
    if (base.excluded) return { ...base, reason: rewriteExclusionReason(name, base.reason) };
    return base;
  }

  function convertBase(name) {
    if (!baseResults.has(name)) {
      // 循環参照は計算中の印で検出して除外する
      // Cycles are detected by the in-progress marker and excluded
      baseResults.set(name, excluded("循環参照"));
      baseResults.set(name, convertUncached(name));
    }
    return baseResults.get(name);
  }

  function convertUncached(name) {
    if (!rawByName.has(name)) return excluded(`未定義のトークン ${name} を参照`);
    const raw = rawByName.get(name);
    if (viewportNamePattern.test(name)) return { excluded: true, viewport: true, reason: viewportReason };
    const compound = convertCompound(name, raw, resolveNumber);
    if (compound !== null) return compound;
    const referenceFailure = findExcludedReference(raw);
    if (referenceFailure !== null) return referenceFailure;
    if (/^var\(--[\w-]+\)$/.test(raw)) return { value: raw };
    if (raw.startsWith("calc(")) return convertCalc(raw);
    // hex はそのまま、関数表記はカンマ区切りの rgb()/rgba() へ事前計算する
    // Hex stays as is; functional notations are precomputed into comma-separated rgb()/rgba()
    if (raw.startsWith("#")) return parseColor(raw, resolveColor) === null ? excluded("色として解釈できない") : { value: raw };
    if (/^(rgba?\(|color-mix\()/.test(raw)) return convertColor(raw);
    return convertScalar(name, raw);
  }

  // 参照先が除外ならこのトークンも除外する。ビューポート依存は理由を引き継ぐ
  // A token referencing an excluded token is excluded too; viewport dependence carries its reason
  function findExcludedReference(raw) {
    for (const match of raw.matchAll(/var\((--[\w-]+)\)/g)) {
      const referenced = convertBase(match[1]);
      if (!referenced.excluded) continue;
      if (referenced.viewport) return { excluded: true, viewport: true, reason: `${viewportReason}（${match[1]} に依存）` };
      return excluded(`参照先 ${match[1]} が除外`);
    }
    return null;
  }

  function convertCalc(raw) {
    const body = raw.slice("calc(".length, -1);
    const result = evaluateCalc(body, resolveNumber);
    if (result === null) return excluded("calc を数値に評価できない");
    return { value: `${formatNumber(result.value)}${result.unit}`, comment: `calc: ${body}` };
  }

  function convertColor(raw) {
    const color = parseColor(raw, resolveColor);
    if (color === null) return excluded("色として解釈できない");
    return { value: formatColor(color) };
  }

  function convertScalar(name, raw) {
    const numberMatch = raw.match(/^(-?\d*\.?\d+)([a-z%]*)$/);
    if (numberMatch === null) return excluded(classifyNonScalar(raw));
    const [, number, unit] = numberMatch;
    if (unit === "em") return excluded("em は親の文字サイズ依存。部品の段で px に決める");
    if (unit === "rem") return { value: `${formatNumber(Number(number) * 16)}px` };
    if (["px", "%", "ms", "s", "deg"].includes(unit)) return { value: raw };
    if (unit !== "") return excluded(`USS に無い単位 ${unit}`);
    // z 層は C# の層 enum が正本（USS に z-index は無い）
    // Z layers live in the C# layer enum (USS has no z-index)
    if (name.startsWith("--z-")) return excluded("z-index は C# の層 enum へ写す（USS に z-index は無い）");
    return { value: raw };
  }

  // 解決後の値を数値へ。var だけの値は参照先をたどる
  // Resolves a token to a number, following var-only values
  function resolveNumber(name) {
    const result = convertBase(name);
    if (result.excluded || result.value === undefined) return null;
    const varMatch = result.value.match(/^var\((--[\w-]+)\)$/);
    if (varMatch) return resolveNumber(varMatch[1]);
    const numberMatch = result.value.match(/^(-?\d*\.?\d+)([a-z%]*)$/);
    return numberMatch === null ? null : numberWithUnit(Number(numberMatch[1]), numberMatch[2]);
  }

  function resolveColor(name) {
    const result = convertBase(name);
    return result.excluded || result.value === undefined ? null : parseColor(result.value, resolveColor);
  }
}

function excluded(reason) {
  return { excluded: true, viewport: false, reason };
}

// 除外理由を行き先の分類語（層 enum・実行時の寸法・UiMotion・フォントアセット）で始まる形へ揃える
// Rewrites exclusion reasons to start with their destination category (layer enum, runtime size, UiMotion, font asset)
function rewriteExclusionReason(name, reason) {
  if (name === "--font-ui") return "フォントアセット: Typography.uss の -unity-font-definition（GameUi{Latin,Japanese,Korean} SDF）";
  if (name.startsWith("--tutorial-pulse-")) return "UiMotion: @keyframes 名は UiMotion の尺トークンで置き換える";
  if (name.startsWith("--z-")) return "層 enum: UiLayer";
  return reason;
}

function classifyNonScalar(raw) {
  if (/^["']/.test(raw)) return "文字列（フォント族）。USS は -unity-font-definition のアセット参照で指定する";
  if (/^[a-z-]+\(/.test(raw)) return `USS に無い関数 ${raw.slice(0, raw.indexOf("("))}()`;
  if (/^[a-z-]+$/.test(raw)) return "CSS 固有の識別子（@keyframes 名）。UiMotion で置き換える";
  return "複合値（影・境界線・余白の一括指定）。部品の段で個別プロパティへ分ける";
}
