// 1 つの CSS 値が複数の USS プロパティを兼ねる複合トークン（em・影・余白・境界線・グラデーション）を変換する
// Converts compound tokens whose single CSS value spans several USS properties (em, shadow, padding, border, gradient)
import { formatNumber } from "./parseCssTokens.mjs";
import { formatColor, parseColor } from "./cssColors.mjs";

// em の letter-spacing は同じ部品の文字寸法トークンに掛けて px にする
// An em letter-spacing is multiplied by the same component's text-size token to become px
const emBaseTokenByName = { "--key-hint-letter-spacing": "--key-hint-font-size" };
const textShadowNames = new Set(["--key-hint-text-shadow", "--challenge-hud-text-shadow"]);
const passThroughNames = new Set(["--label-face-padding"]);
const borderSplitNames = new Set(["--tooltip-border"]);
const gradientSplitNames = new Set(["--recipe-action-background"]);
const gradientSuffixes = ["start", "middle", "end"];
const dropShadowReason = "filter: drop-shadow は USS に無い。部品の段でメッシュで描く";

// 複合トークンなら変換結果を返し、そうでなければ null を返す。derived は分割で生まれた派生トークン
// Returns the conversion for a compound token or null; derived lists the tokens born from a split
export function convertCompound(name, raw, resolveNumber) {
  if (name in emBaseTokenByName) return convertEmLetterSpacing(name, raw, resolveNumber);
  if (textShadowNames.has(name)) return convertTextShadow(raw);
  if (passThroughNames.has(name)) return { value: raw };
  if (borderSplitNames.has(name)) return splitBorder(name, raw);
  if (gradientSplitNames.has(name)) return splitGradient(name, raw);
  if (name === "--skit-tool-icon-shadow") return { excluded: true, viewport: false, reason: dropShadowReason };
  return null;
}

function convertEmLetterSpacing(name, raw, resolveNumber) {
  const emMatch = raw.match(/^(-?\d*\.?\d+)em$/);
  const base = resolveNumber(emBaseTokenByName[name]);
  if (emMatch === null || base === null) return { excluded: true, viewport: false, reason: `em の基準 ${emBaseTokenByName[name]} を解決できない` };
  const value = `${formatNumber(Number(emMatch[1]) * base.value)}px`;
  return { value, comment: `em: ${raw} × var(${emBaseTokenByName[name]})` };
}

function convertTextShadow(raw) {
  const match = raw.match(/^(\S+ \S+ \S+) (rgba?\(.+\))$/);
  const color = match === null ? null : parseColor(match[2], () => null);
  if (color === null) return { excluded: true, viewport: false, reason: `影の色を解釈できない: ${raw}` };
  return { value: `${match[1]} ${formatColor(color)}`, comment: `web: ${raw}` };
}

function splitBorder(name, raw) {
  const match = raw.match(/^(\S+) solid (rgba?\(.+\))$/);
  const color = match === null ? null : parseColor(match[2], () => null);
  if (color === null) return { excluded: true, viewport: false, reason: `境界線を幅と色へ分けられない: ${raw}` };
  return { derived: [
    { name: `${name}-width`, value: match[1] },
    { name: `${name}-color`, value: formatColor(color) },
  ] };
}

function splitGradient(name, raw) {
  const stops = [...raw.matchAll(/(#[0-9a-fA-F]{3,8}) \d+%/g)].map((match) => match[1]);
  if (!raw.startsWith("linear-gradient(") || stops.length !== gradientSuffixes.length) {
    return { excluded: true, viewport: false, reason: `グラデーションを 3 点へ分けられない: ${raw}` };
  }
  return { derived: stops.map((stop, index) => ({ name: `${name}-${gradientSuffixes[index]}`, value: stop })) };
}
