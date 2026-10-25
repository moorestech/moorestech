import { execFileSync } from "node:child_process";
import { join } from "node:path";
import type { Browser } from "@playwright/test";
import type { ShotSetup } from "./referenceShots";
import { finishShot, prepareShot, referenceViewport } from "./shotRunner";

// 準備待ちを尺から切り離し、演出開始から指定秒数を保存する
// Exclude setup latency and retain the requested duration from the effect's start
export async function captureVideo(browser: Browser, baseUrl: string, setup: ShotSetup, durationMs: number, outputPath: string, temporaryDirectory: string): Promise<void> {
  const context = await browser.newContext({ viewport: referenceViewport, recordVideo: { dir: temporaryDirectory, size: referenceViewport } });
  const page = await context.newPage();
  const recording = page.video();
  if (recording === null) throw new Error(`video was not recorded: ${outputPath}`);
  try {
    await prepareShot(page, baseUrl, setup);
    const firstAction = setup.steps.findIndex((step) => step.kind !== "expectVisible" && step.kind !== "expectContent");
    const readyCount = firstAction === -1 ? setup.steps.length : firstAction;
    await finishShot(page, baseUrl, { controls: [], steps: setup.steps.slice(0, readyCount) });
    const effectStartedAt = performance.now();
    // エンコーダ終了時の微小なずれでも入場の先頭を残す
    // Keep entry frames despite small encoder shutdown drift
    const effectLeadInMs = 300;
    await page.waitForTimeout(effectLeadInMs);
    await finishShot(page, baseUrl, { controls: [], steps: setup.steps.slice(readyCount) });
    const remainingMs = durationMs - (performance.now() - effectStartedAt);
    if (remainingMs <= 0) throw new Error(`video actions exceeded duration: ${outputPath}`);
    await page.waitForTimeout(remainingMs);
  } finally {
    await context.close();
  }
  // 録画末尾の指定尺を再符号化し、起動中の空白フレームを除く
  // Re-encode the requested tail duration to remove startup blank frames
  const rawPath = join(temporaryDirectory, "untrimmed.webm");
  await recording.saveAs(rawPath);
  execFileSync("ffmpeg", ["-hide_banner", "-loglevel", "error", "-y", "-sseof", String(-durationMs / 1000), "-i", rawPath,
    "-t", String(durationMs / 1000), "-an", "-c:v", "libvpx-vp9", "-deadline", "realtime", "-cpu-used", "8", outputPath]);
  await recording.delete();
}
