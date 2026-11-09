// Linear 色空間の UITK は sRGB で混ぜる Chrome と半透明の濃さが違うため、α を近似補正する（S7。docs/uitk/TODO.md）
// UITK in Linear color space blends translucency differently from Chrome's sRGB blend, so approximate-correct the alpha (S7, docs/uitk/TODO.md)

// 指数はランタイムの PanelSettings（Linear 合成）で地に載せた色を Web と比べて決めた。暗い色は地 30〜180（C2-a・I-fix1）、明るい色は UI の暗い面の地（16〜35。I-fix2）で 1.85 が合う（2.2 はどちらも外れた）
// Exponents were fitted on a runtime PanelSettings (Linear blend) against the Web: dark colors over grounds 30-180 (C2-a, I-fix1), light colors over the dark UI faces they sit on (16-35, I-fix2); 1.85 fits both (2.2 missed both)
export const darkExponent = 1.85;
const lightExponent = 1.85;

// 明暗を分ける最大成分の境（0〜1）
// Largest-channel boundary between dark and light colors (0 to 1)
export const lightDarkSplitChannel = 0.5;

// 明暗は最大の成分で分ける。純色の赤（輝度 0.21）は暗い地の上で明るい色として振る舞い、輝度で分けると 87 段外れた（I-fix2 実測）
// Light versus dark is split by the largest channel: pure red (luminance 0.21) behaves as a light color over dark ground and missed by 87 steps when split by luminance (I-fix2)
export function correctAlphaForLinearBlend(red, green, blue, alpha) {
  if (alpha <= 0 || alpha >= 1) return alpha;
  const brightest = Math.max(red, green, blue) / 255;
  const corrected = brightest < lightDarkSplitChannel ? 1 - Math.pow(1 - alpha, darkExponent) : Math.pow(alpha, lightExponent);
  return Math.round(corrected * 10000) / 10000;
}

export const translucencyMethod = `α 近似補正（S7。最大成分 ${lightDarkSplitChannel} 未満の暗い色 1-(1-α)^${darkExponent}・明るい色 α^${lightExponent}）`;

// 全面に敷く黒の幕は地に一定の比を掛けるだけなので、地の明るさに依らず 1 つの指数で寄せられる。参照 3-modal と 2-game-hud の同じ画素 400 点で最大差・平均差が最小の指数（I-fix3 実測。1.85 は最大 16 段・平均 8.5 段、2.15 は最大 6 段・平均 2.4 段）
// A full-screen black dim only multiplies the ground by a constant, so one exponent fits regardless of ground brightness; this exponent minimised the max and mean error over 400 matching pixels of references 3-modal and 2-game-hud (I-fix3: 1.85 gave max 16 / mean 8.5 steps, 2.15 gives max 6 / mean 2.4)
// 最大差が 2 段に入らないのは、Linear 合成の 1 つの α では sRGB の掛け算を暗い地（sRGB の直線部）と明るい地で同時に再現できないため（look-exception: LX-46）
// The max error cannot reach 2 steps because a single alpha under Linear blending cannot reproduce an sRGB multiply on both dark grounds (the linear toe) and bright ones (look-exception: LX-46)
export const blackDimExponent = 2.15;

export function correctBlackDimAlpha(alpha) {
  if (alpha <= 0 || alpha >= 1) return alpha;
  return Math.round((1 - Math.pow(1 - alpha, blackDimExponent)) * 10000) / 10000;
}

export const blackDimMethod = `黒の幕の α 補正（1-(1-α)^${blackDimExponent}。地に比を掛けるだけの黒の全面の幕に限る）`;
