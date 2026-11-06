// 確定した USS の値へ後処理（半透明の α 補正）を当て、半透明の表の行を積む。文字寸法は Web と同じ px のまま（設計書 A-39）
// Applies post-processing (translucent alpha correction) to a settled USS value and records translucent-table rows; text sizes stay at the Web's px (design A-39)
import { correctAlphaForLinearBlend } from "./translucency.mjs";
import { formatColor, parseColor } from "./cssColors.mjs";

export function postProcessToken(name, result, translucentRows) {
  if (!/^rgba\(/.test(result.value)) return result;
  const color = parseColor(result.value, () => null);
  if (color === null || color.a <= 0 || color.a >= 1) return result;
  const corrected = { ...color, a: correctAlphaForLinearBlend(color.r, color.g, color.b, color.a) };
  const ussValue = formatColor(corrected);
  translucentRows.push({ name, webValue: result.value, ussValue });
  const comment = result.comment === undefined ? `web: ${result.value} α補正` : `${result.comment}; web: ${result.value} α補正`;
  return { ...result, value: ussValue, comment };
}
