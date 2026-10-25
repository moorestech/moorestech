import { execFileSync } from "node:child_process";
import { mkdir, readdir, rm } from "node:fs/promises";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "@playwright/test";
import { writeCaptureIndex, type CaptureEntry } from "./captureIndex";
import { referenceShots } from "./referenceShots";
import { referenceVideos } from "./referenceVideos";
import { openShot, referenceViewport, startMockHost, stopMockHost } from "./shotRunner";

const webuiRoot = fileURLToPath(new URL("../..", import.meta.url));
const port = Number(process.env.UITK_REFERENCE_PORT ?? 5390);
const baseUrl = `http://127.0.0.1:${port}`;

async function main(): Promise<void> {
  // 出力先は必須。既存の撮影と混ざらないよう空のディレクトリだけを受ける
  // The output directory is required and must be empty so runs never mix
  const outputDirectory = process.env.UITK_REFERENCE_OUT;
  if (outputDirectory === undefined) throw new Error("UITK_REFERENCE_OUT is required");
  await mkdir(outputDirectory, { recursive: true });
  if ((await readdir(outputDirectory)).length > 0) throw new Error(`output directory is not empty: ${outputDirectory}`);
  const sourceCommit = execFileSync("git", ["rev-parse", "--short=10", "HEAD"], { cwd: webuiRoot, encoding: "utf8" }).trim();
  const entries: CaptureEntry[] = [];
  const browser = await chromium.launch();
  // ブラウザは成功・失敗のどちらでも閉じる
  // Close the browser on both the success and the failure path
  try {

    // 静止画: 撮影ごとに mock-host と context を作り直す
    // Stills: recreate the mock-host and the context for every shot
    for (const shot of referenceShots) {
      const host = await startMockHost(webuiRoot, port);
      // 失敗しても子プロセスと context を残さない（前例 challenge-hud/capture.ts の finally と同じ）
      // Never leak the child process or the context on failure (same as the finally in challenge-hud/capture.ts)
      try {
        const context = await browser.newContext({ viewport: referenceViewport });
        const page = await context.newPage();
        await openShot(page, baseUrl, shot);
        await page.screenshot({ path: join(outputDirectory, `${shot.id}.png`) });
        await context.close();
      } finally {
        await stopMockHost(host);
      }
      entries.push({ id: shot.id, bundle: shot.bundle, ledgerRow: shot.ledgerRow, file: `${shot.id}.png`, kind: "image" });
    }

    // 動画: recordVideo の context で尺だけ待ち、閉じてから保存する
    // Videos: wait the duration inside a recordVideo context, then save after closing it
    for (const video of referenceVideos) {
      const host = await startMockHost(webuiRoot, port);
      try {
        const context = await browser.newContext({ viewport: referenceViewport, recordVideo: { dir: join(outputDirectory, ".video-tmp"), size: referenceViewport } });
        const recordingStartedAt = performance.now();
        const page = await context.newPage();
        await openShot(page, baseUrl, video);
        const remainingMs = video.durationMs - (performance.now() - recordingStartedAt);
        if (remainingMs <= 0) throw new Error(`video setup exceeded duration: ${video.id}`);
        await page.waitForTimeout(remainingMs);
        const recording = page.video();
        await context.close();
        if (recording === null) throw new Error(`video was not recorded: ${video.id}`);
        await recording.saveAs(join(outputDirectory, `${video.id}.webm`));
        await recording.delete();
      } finally {
        await stopMockHost(host);
      }
      entries.push({ id: video.id, bundle: video.bundle, ledgerRow: video.ledgerRow, file: `${video.id}.webm`, kind: "video" });
    }
  } finally {
    await browser.close();
  }
  await rm(join(outputDirectory, ".video-tmp"), { recursive: true, force: true });
  await writeCaptureIndex(outputDirectory, entries, sourceCommit);
}

// 外部境界（ブラウザ・子プロセス）の失敗を終了コードへ変える
// Convert external-boundary failures (browser, child processes) into an exit code
void main().catch((error: unknown) => {
  console.error(error);
  process.exitCode = 1;
});
