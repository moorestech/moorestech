// Linear 色空間の UITK は sRGB で混ぜる Chrome と半透明の濃さが違うため、α を近似補正する（S7。docs/uitk/TODO.md）
// UITK in Linear color space blends translucency differently from Chrome's sRGB blend, so approximate-correct the alpha (S7, docs/uitk/TODO.md)

// 暗い色の指数はランタイムの PanelSettings（Linear 合成）でゲームの地に載せた面を Web と比べて決めた（C2-a・I-fix1 実測。2.2 は地 30〜180 のどこでも濃すぎた）
// The dark exponent was fitted by comparing faces over game backdrops on a runtime PanelSettings (Linear blend) to the Web (C2-a and I-fix1; 2.2 was too dark on every backdrop from 30 to 180)
export const darkExponent = 1.85;
const lightExponent = 2.2;

// 暗い色は 1-(1-α)^1.85、明るい色は α^2.2（明るい色は未実測の初期値）
// Dark colors use 1-(1-α)^1.85 and light colors α^2.2 (the light rule is an unmeasured starting value)
export function correctAlphaForLinearBlend(red, green, blue, alpha) {
  if (alpha <= 0 || alpha >= 1) return alpha;
  const luminance = (0.2126 * red + 0.7152 * green + 0.0722 * blue) / 255;
  const corrected = luminance < 0.5 ? 1 - Math.pow(1 - alpha, darkExponent) : Math.pow(alpha, lightExponent);
  return Math.round(corrected * 10000) / 10000;
}

export const translucencyMethod = "α 近似補正（S7。暗い色 1-(1-α)^1.85・明るい色 α^2.2）";
