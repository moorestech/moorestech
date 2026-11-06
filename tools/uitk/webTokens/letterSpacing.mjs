// 字間トークンを UITK の letter-spacing の値へ換算する。UITK は px と書いても「字の大きさ/100 × 値」で効く（C2-a・C2-c 実測）
// Converts letter-spacing tokens to UITK values; UITK applies letter-spacing as font size / 100 x value even when written in px (measured in C2-a and C2-c)
import { formatNumber } from "./parseCssTokens.mjs";

// px の字間は、その字間を当てる文字の大きさのトークンで割る。Web の CSS で同じ宣言に並ぶ font-size を書く
// A px letter-spacing is divided by the font size it is applied with; list the font-size declared next to it in the Web CSS
const fontSizeTokenByPixelLetterSpacing = {
  "--challenge-hud-letter-spacing": "--challenge-hud-label-font-size",
  "--operation-hud-letter-spacing": "--operation-hud-label-font-size",
};
const uitkUnitsPerEm = 100;

export function isLetterSpacingToken(name) {
  return name.endsWith("-letter-spacing");
}

export function convertLetterSpacing(name, raw, resolveNumber) {
  const match = raw.match(/^(-?\d*\.?\d+)(em|px)?$/);
  if (match === null) throw new Error(`${name}: letter-spacing '${raw}' is neither em nor px`);
  const amount = Number(match[1]);
  // em は字の大きさに依らず ×100 で同じ字間になる
  // An em value maps to x100 regardless of the font size
  if (match[2] === "em") return { value: `${formatNumber(amount * uitkUnitsPerEm)}px`, comment: `web: ${raw}。UITK の字間は字の大きさの 1/100 単位` };
  if (amount === 0) return { value: "0px" };
  // px は使う字の大きさが無いと換算できないので、黙って px のまま出さずに止める
  // A px value needs its font size, so stop instead of silently emitting it unconverted
  const fontSizeToken = fontSizeTokenByPixelLetterSpacing[name];
  if (fontSizeToken === undefined) throw new Error(`${name}: px letter-spacing needs an entry in fontSizeTokenByPixelLetterSpacing (letterSpacing.mjs)`);
  const fontSize = resolveNumber(fontSizeToken);
  if (fontSize === null || fontSize.unit !== "px") throw new Error(`${name}: font size ${fontSizeToken} does not resolve to px`);
  const value = `${formatNumber(Math.round((amount / fontSize.value) * uitkUnitsPerEm * 10000) / 10000)}px`;
  return { value, comment: `web: ${raw} ÷ var(${fontSizeToken}) × 100。UITK の字間は字の大きさの 1/100 単位` };
}
