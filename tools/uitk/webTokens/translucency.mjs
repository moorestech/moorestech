// Linear 色空間の UITK は sRGB で混ぜる Chrome と半透明の濃さが違うため、α を近似補正する（S7。docs/uitk/TODO.md）
// UITK in Linear color space blends translucency differently from Chrome's sRGB blend, so approximate-correct the alpha (S7, docs/uitk/TODO.md)

// 指数はランタイムの PanelSettings（Linear 合成）で地に載せた色を Web と比べて決めた。暗い色は地 30〜180（C2-a・I-fix1）、明るい色は UI の暗い面の地（16〜35。I-fix2）で 1.85 が合う（2.2 はどちらも外れた）
// Exponents were fitted on a runtime PanelSettings (Linear blend) against the Web: dark colors over grounds 30-180 (C2-a, I-fix1), light colors over the dark UI faces they sit on (16-35, I-fix2); 1.85 fits both (2.2 missed both)
export const darkExponent = 1.85;
const lightExponent = 1.85;

// 明暗は最大の成分で分ける。純色の赤（輝度 0.21）は暗い地の上で明るい色として振る舞い、輝度で分けると 87 段外れた（I-fix2 実測）
// Light versus dark is split by the largest channel: pure red (luminance 0.21) behaves as a light color over dark ground and missed by 87 steps when split by luminance (I-fix2)
export function correctAlphaForLinearBlend(red, green, blue, alpha) {
  if (alpha <= 0 || alpha >= 1) return alpha;
  const brightest = Math.max(red, green, blue) / 255;
  const corrected = brightest < 0.5 ? 1 - Math.pow(1 - alpha, darkExponent) : Math.pow(alpha, lightExponent);
  return Math.round(corrected * 10000) / 10000;
}

export const translucencyMethod = `α 近似補正（S7。最大成分 0.5 未満の暗い色 1-(1-α)^${darkExponent}・明るい色 α^${lightExponent}）`;
