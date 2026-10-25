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
