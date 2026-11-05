// 確定した USS の値へ後処理（半透明の α 補正・文字寸法の下限）を当て、半透明の表の行を積む
// Applies post-processing (translucent alpha correction, text-size floor) to a settled USS value and records translucent-table rows
import { applyFontSizeFloor, isFontSizeToken } from "./fontSizeFloor.mjs";
import { correctAlphaForLinearBlend } from "./translucency.mjs";
import { formatColor, parseColor } from "./cssColors.mjs";

export function postProcessToken(name, result, translucentRows) {
  const afterAlpha = correctTranslucency(name, result, translucentRows);
  return raiseFontSizeToFloor(name, afterAlpha);
}

function joinComments(existing, added) {
  return existing === undefined ? added : `${existing}; ${added}`;
}

function correctTranslucency(name, result, translucentRows) {
  if (!/^rgba\(/.test(result.value)) return result;
  const color = parseColor(result.value, () => null);
  if (color === null || color.a <= 0 || color.a >= 1) return result;
  const corrected = { ...color, a: correctAlphaForLinearBlend(color.r, color.g, color.b, color.a) };
  const ussValue = formatColor(corrected);
  translucentRows.push({ name, webValue: result.value, ussValue });
  return { ...result, value: ussValue, comment: joinComments(result.comment, `web: ${result.value} α補正`) };
}

function raiseFontSizeToFloor(name, result) {
  const pxMatch = result.value.match(/^(-?\d*\.?\d+)px$/);
  if (!isFontSizeToken(name) || pxMatch === null) return result;
  const floored = applyFontSizeFloor(name, Number(pxMatch[1]));
  if (!floored.raised) return result;
  return { ...result, value: `${floored.px}px`, comment: joinComments(result.comment, `web: ${result.value} 下限${floored.px}px`) };
}
