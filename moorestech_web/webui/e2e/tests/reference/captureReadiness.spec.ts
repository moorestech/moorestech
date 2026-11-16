import { execFileSync } from "node:child_process";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium, type Browser, expect, test } from "@playwright/test";
import { openBlockFixtureNames } from "../../mock-host/httpHandler";
import { referenceShots, content, ui, topic, visible } from "../../uitk-reference/referenceShots";
import { referenceVideos } from "../../uitk-reference/referenceVideos";
import { openShot, startMockHost, stopMockHost } from "../../uitk-reference/shotRunner";
import { captureVideo } from "../../uitk-reference/videoCapture";

const root = fileURLToPath(new URL("../../..", import.meta.url));
const port = 5392;
const baseUrl = `http://127.0.0.1:${port}`;
let browser: Browser;
test.beforeAll(async () => { browser = await chromium.launch(); });
test.afterAll(async () => { await browser.close(); });

// 撮影定義の全対象を実DOMで検査する
// Exercise every still's readiness contract against the real DOM
test.setTimeout(25_000);
for (const shot of referenceShots) test(`${shot.id} reaches its visible state`, async () => {
  const host = await startMockHost(root, port);
  const page = await browser.newPage();
  try {
    await openShot(page, baseUrl, shot);
  } finally {
    await page.close();
    await stopMockHost(host);
  }
});

test("every capture has expectations and block fixtures are derived", () => {
  for (const setup of [...referenceShots, ...referenceVideos]) {
    expect(setup.steps.some((step) => step.kind === "expectVisible" || step.kind === "expectContent")).toBe(true);
  }
  expect(referenceShots.filter((shot) => shot.controls.some((path) => path.startsWith("/__block")))
    .slice(1, 1 + openBlockFixtureNames.length).map((shot) => shot.id.replace("4-block-", ""))).toEqual(openBlockFixtureNames);
});

// 遅いsnapshotが固定600msを超えても本文と件数の成立まで待つ
// A snapshot slower than the old 600ms delay must complete with the expected count and content
test("delayed snapshot waits for content, mismatched content fails", async () => {
  const host = await startMockHost(root, port);
  const page = await browser.newPage();
  try {
    await openShot(page, baseUrl, {
      controls: ["/__snapshot-delay?ms=1800", topic("progressLabeled"), ui("GameScreen")],
      steps: [content("progress-bar", ["Crafting"])],
    });
    expect(await page.getByTestId("progress-bar").textContent()).toContain("Crafting");
    await expect(openShot(page, baseUrl, { controls: [], steps: [content("progress-bar", ["missing"])] })).rejects.toThrow();
  } finally {
    await page.close();
    await stopMockHost(host);
  }
});

// 準備が指定尺を超えても短い演出動画を丸ごと残す
// Preserve the full effect duration even when setup exceeds the clip length
test("video excludes a four-second snapshot setup from a three-second clip", async () => {
  const host = await startMockHost(root, port);
  const directory = await mkdtemp(join(tmpdir(), "uitk-video-ready-"));
  try {
    const output = join(directory, "delayed.webm");
    await captureVideo(browser, baseUrl, {
      controls: ["/__snapshot-delay?ms=4000", topic("tutorialKeyControl"), ui("GameScreen")],
      steps: [visible("key-control-hint")],
    }, 3000, output, join(directory, "raw"));
    const seconds = Number(execFileSync("ffprobe", ["-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", output], { encoding: "utf8" }));
    expect(seconds).toBeGreaterThan(2);
    expect(seconds).toBeLessThan(4);
  } finally {
    await stopMockHost(host);
    await rm(directory, { recursive: true });
  }
});

// 最初と最後は空の通知層、途中は通知ありを実動画の画素で確かめる
// Verify an empty notification layer at both ends and visible content in the encoded middle
test("notification clip retains entry and complete exit", async () => {
  const host = await startMockHost(root, port);
  const directory = await mkdtemp(join(tmpdir(), "uitk-notification-"));
  const baselinePage = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  try {
    await openShot(baselinePage, baseUrl, { controls: [ui("GameScreen")], steps: [visible("hotbar-grid")] });
    const baseline = join(directory, "empty.png");
    await baselinePage.screenshot({ path: baseline });
    await baselinePage.close();
    const video = referenceVideos.find((entry) => entry.id === "2-notification-enter-aggregate-exit")!;
    const output = join(directory, "notifications.webm");
    await captureVideo(browser, baseUrl, video, video.durationMs, output, join(directory, "raw"));
    const expected = notificationPixels(baseline, 0);
    expect(pixelDifference(expected, notificationPixels(output, 0))).toBeLessThan(3);
    expect(pixelDifference(expected, notificationPixels(output, 1.2))).toBeGreaterThan(5);
    expect(pixelDifference(expected, notificationPixels(output, video.durationMs / 1000 - 0.2))).toBeLessThan(3);
  } finally {
    await baselinePage.close();
    await stopMockHost(host);
    await rm(directory, { recursive: true });
  }
});

function notificationPixels(path: string, seconds: number): Buffer {
  return execFileSync("ffmpeg", ["-v", "error", "-ss", String(seconds), "-i", path, "-frames:v", "1",
    "-vf", "crop=440:140:0:350,format=gray", "-f", "rawvideo", "-"]);
}

function pixelDifference(expected: Buffer, actual: Buffer): number {
  expect(actual.length).toBe(expected.length);
  let total = 0;
  for (let index = 0; index < actual.length; index += 1) total += Math.abs(actual[index] - expected[index]);
  return total / actual.length;
}

// 動画の定義自身が脈動する対象を選んでいることを検査する
// Verify the video definition selects the target that actually pulses
test("world pin reference selects the animated off-screen arrow", async () => {
  const host = await startMockHost(root, port);
  const page = await browser.newPage();
  try {
    const video = referenceVideos.find((entry) => entry.id === "2-world-pin-pulse")!;
    await openShot(page, baseUrl, video);
    const transforms = await page.getByTestId("world-pin-arrow-map-object-pin").locator("svg").evaluate((svg) =>
      svg.getAnimations().flatMap((animation) => (animation.effect as KeyframeEffect).getKeyframes().map((frame) => String(frame.transform))));
    expect(transforms).toContain("scale(1.08)");
  } finally {
    await page.close();
    await stopMockHost(host);
  }
});
