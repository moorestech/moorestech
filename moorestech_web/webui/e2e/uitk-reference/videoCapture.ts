import { execFileSync } from "node:child_process";
import { join } from "node:path";
import type { Browser, Page } from "@playwright/test";
import type { ShotSetup } from "./referenceShots";
import { finishShot, prepareShot, referenceViewport } from "./shotRunner";

const MARKER_HOLD_MS = 400;
const EFFECT_LEAD_IN_MS = 500;
const ENCODER_TAIL_MS = 500;

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
    await markRecordingBoundary(page);
    const effectStartedAt = performance.now();
    await page.waitForTimeout(EFFECT_LEAD_IN_MS);
    await finishShot(page, baseUrl, { controls: [], steps: setup.steps.slice(readyCount) });
    const remainingMs = durationMs - (performance.now() - effectStartedAt);
    if (remainingMs <= 0) throw new Error(`video actions exceeded duration: ${outputPath}`);
    await page.waitForTimeout(remainingMs + ENCODER_TAIL_MS);
  } finally {
    await context.close();
  }
  // 録画フレームの境界から切り出し、エンコーダ終了待ちで先頭が欠けるのを防ぐ
  // Trim from the encoded boundary so encoder shutdown latency cannot remove entry frames
  const rawPath = join(temporaryDirectory, "untrimmed.webm");
  await recording.saveAs(rawPath);
  const startSeconds = recordingBoundarySeconds(rawPath);
  execFileSync("ffmpeg", ["-hide_banner", "-loglevel", "error", "-y", "-i", rawPath,
    "-ss", String(startSeconds), "-t", String(durationMs / 1000), "-an", "-c:v", "libvpx-vp9", "-deadline", "realtime", "-cpu-used", "8", outputPath]);
  await recording.delete();
}

// 緑の小片は準備区間にだけ表示し、最終動画の開始位置を録画自身へ刻む
// Show a green patch only during setup to encode the final clip boundary into the recording
async function markRecordingBoundary(page: Page): Promise<void> {
  await page.evaluate(() => {
    const marker = document.createElement("div");
    marker.id = "capture-boundary-marker";
    marker.style.cssText = "position:fixed;left:0;top:0;width:16px;height:16px;background:rgb(0,255,0);z-index:2147483647";
    document.body.append(marker);
  });
  await page.waitForTimeout(MARKER_HOLD_MS);
  await page.evaluate(() => document.getElementById("capture-boundary-marker")!.remove());
}

function recordingBoundarySeconds(path: string): number {
  const rate = execFileSync("ffprobe", ["-v", "error", "-select_streams", "v:0", "-show_entries", "stream=avg_frame_rate", "-of", "default=nw=1:nk=1", path], { encoding: "utf8" }).trim();
  const [numerator, denominator] = rate.split("/").map(Number);
  const fps = numerator / denominator;
  if (!Number.isFinite(fps) || fps <= 0) throw new Error(`invalid recording frame rate: ${rate}`);
  const pixels = execFileSync("ffmpeg", ["-v", "error", "-i", path, "-vf", "crop=2:2:4:4,scale=1:1,format=rgb24", "-f", "rawvideo", "-"]);
  let lastMarkerFrame = -1;
  for (let index = 0; index < pixels.length; index += 3) {
    if (pixels[index] < 50 && pixels[index + 1] > 200 && pixels[index + 2] < 50) lastMarkerFrame = index / 3;
  }
  if (lastMarkerFrame < 0) throw new Error(`recording boundary marker is missing: ${path}`);
  return (lastMarkerFrame + 1) / fps;
}
