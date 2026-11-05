// CSS の色表記を {r,g,b,a} に解き、USS が読める rgb()/rgba() のカンマ区切りへ書き直す
// Parses CSS color notations into {r,g,b,a} and rewrites them as comma-separated rgb()/rgba() USS can read
// resolveVarColor(name) は参照トークンを色へ解決し、できなければ null を返す
// resolveVarColor(name) resolves a referenced token to a color, or returns null
import { formatNumber } from "./parseCssTokens.mjs";

export function parseColor(text, resolveVarColor) {
  const value = text.trim();
  const varMatch = value.match(/^var\((--[\w-]+)\)$/);
  if (varMatch) return resolveVarColor(varMatch[1]);
  if (value === "transparent") return { r: 0, g: 0, b: 0, a: 0 };
  if (value.startsWith("#")) return parseHex(value);
  const relativeMatch = value.match(/^rgb\(from (.+) r g b(?: \/ ([\d.]+%?))?\)$/);
  if (relativeMatch) return parseRelativeColor(relativeMatch, resolveVarColor);
  const mixMatch = value.match(/^color-mix\(in srgb, (.+)\)$/);
  if (mixMatch) return parseColorMix(mixMatch[1], resolveVarColor);
  const rgbMatch = value.match(/^rgba?\(([^()]+)\)$/);
  if (rgbMatch) return parseRgbArguments(rgbMatch[1]);
  return null;
}

export function formatColor(color) {
  const channels = [color.r, color.g, color.b].map((channel) => formatNumber(Math.round(channel)));
  if (color.a >= 1) return `rgb(${channels.join(", ")})`;
  return `rgba(${channels.join(", ")}, ${formatNumber(color.a)})`;
}

function parseHex(value) {
  let hex = value.slice(1);
  if (hex.length === 3 || hex.length === 4) hex = [...hex].map((digit) => digit + digit).join("");
  if (!/^[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$/.test(hex)) return null;
  const channel = (index) => parseInt(hex.slice(index, index + 2), 16);
  return { r: channel(0), g: channel(2), b: channel(4), a: hex.length === 8 ? channel(6) / 255 : 1 };
}

function parseAlpha(text) {
  return text.endsWith("%") ? Number(text.slice(0, -1)) / 100 : Number(text);
}

// `r g b / a` と `r, g, b, a` の両方の並びを受ける
// Accepts both `r g b / a` and `r, g, b, a` argument layouts
function parseRgbArguments(argumentText) {
  const parts = argumentText.split(/[\s,/]+/).filter((part) => part !== "");
  if (parts.length !== 3 && parts.length !== 4) return null;
  const numbers = parts.slice(0, 3).map(Number);
  if (numbers.some((number) => Number.isNaN(number))) return null;
  const a = parts.length === 4 ? parseAlpha(parts[3]) : 1;
  return Number.isNaN(a) ? null : { r: numbers[0], g: numbers[1], b: numbers[2], a };
}

// 相対色は元色の rgb を保ったまま α だけ差し替える
// A relative color keeps the origin's rgb and only replaces alpha
function parseRelativeColor(match, resolveVarColor) {
  const origin = parseColor(match[1], resolveVarColor);
  if (origin === null) return null;
  return { ...origin, a: match[2] === undefined ? origin.a : parseAlpha(match[2]) };
}

// sRGB の color-mix を事前合成する。割合の省略は CSS 仕様どおり残りを補う
// Pre-blends an sRGB color-mix; an omitted percentage takes the remainder as the CSS spec says
function parseColorMix(argumentText, resolveVarColor) {
  const operands = splitTopLevelComma(argumentText);
  if (operands.length !== 2) return null;
  const parsed = operands.map((operand) => {
    const match = operand.trim().match(/^(.+?)(?: ([\d.]+)%)?$/);
    return { color: parseColor(match[1], resolveVarColor), percent: match[2] === undefined ? null : Number(match[2]) };
  });
  if (parsed.some((operand) => operand.color === null)) return null;
  const firstWeight = parsed[0].percent ?? (parsed[1].percent === null ? 50 : 100 - parsed[1].percent);
  const secondWeight = parsed[1].percent ?? 100 - firstWeight;
  const total = firstWeight + secondWeight;
  const [first, second] = parsed.map((operand) => operand.color);
  // 仕様どおり α 前乗算で混ぜ、合計が 100% 未満なら α を縮める
  // Mixes premultiplied per spec and scales alpha down when the total is under 100%
  const alpha = (first.a * firstWeight + second.a * secondWeight) / total;
  const channel = (key) => alpha === 0 ? 0 : (first[key] * first.a * firstWeight + second[key] * second.a * secondWeight) / total / alpha;
  return { r: channel("r"), g: channel("g"), b: channel("b"), a: alpha * Math.min(total, 100) / 100 };
}

function splitTopLevelComma(text) {
  const parts = [];
  let depth = 0;
  let start = 0;
  [...text].forEach((character, index) => {
    if (character === "(") depth++;
    if (character === ")") depth--;
    if (character === "," && depth === 0) {
      parts.push(text.slice(start, index));
      start = index + 1;
    }
  });
  parts.push(text.slice(start));
  return parts;
}
