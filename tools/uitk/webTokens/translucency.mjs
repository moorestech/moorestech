// Linear 色空間の UITK は sRGB で混ぜる Chrome より半透明が薄く出るため、α を近似補正する（S7。docs/uitk/TODO.md）
// UITK in Linear color space blends lighter than Chrome's sRGB blend, so approximate-correct the alpha (S7, docs/uitk/TODO.md)
const gamma = 2.2;

// 暗い色は 1-(1-α)^2.2、明るい色は α^2.2。暗い背景の上で Chrome の見え方に寄る初期値（部品の段で実機で決める）
// Dark colors use 1-(1-α)^2.2 and light colors α^2.2; a starting value close to Chrome on dark backdrops (finalized per component on device)
export function correctAlphaForLinearBlend(red, green, blue, alpha) {
  if (alpha <= 0 || alpha >= 1) return alpha;
  const luminance = (0.2126 * red + 0.7152 * green + 0.0722 * blue) / 255;
  const corrected = luminance < 0.5 ? 1 - Math.pow(1 - alpha, gamma) : Math.pow(alpha, gamma);
  return Math.round(corrected * 10000) / 10000;
}

export const translucencyMethod = "α 近似補正（S7）。部品の段で実機で決める";
