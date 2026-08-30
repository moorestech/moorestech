import type { Page } from "@playwright/test";

// 誘導表示の脈動だけを静止位相(scale=1)へ固定する。尺は殺さず位相だけ止める（webui-design §6）
// Pins only the tutorial pulse to its rest phase (scale=1); the duration stays alive, only the phase stops (webui-design §6)
// 脈動する枠・矢印を実測する検査は、拡大の山に当たると許容pxを超えて位相次第で赤緑が入れ替わるため
// Measuring a pulsing ring or arrow mid-swell exceeds the px tolerance, flipping red/green with the sampling phase
export async function freezeAttentionPulse(page: Page): Promise<void> {
  await page.evaluate(() => {
    for (const animation of document.getAnimations()) {
      const name = (animation as CSSAnimation).animationName;
      if (!name?.startsWith("tutorial-attention-pulse")) continue;
      animation.currentTime = 0;
      animation.pause();
    }
  });
}
