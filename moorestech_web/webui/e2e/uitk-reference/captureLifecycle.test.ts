import { spawn } from "node:child_process";
import { once } from "node:events";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { expect, test } from "vitest";
import { stopMockHost } from "./shotRunner";
import { writeCaptureIndex } from "./captureIndex";

// シグナル終了後でも外側の後片付けへ進む
// Cleanup must reach the outer finally after a signal exit
test.each(["image", "video"])("%s cleanup settles after the host has already exited", async () => {
  const child = spawn(process.execPath, ["-e", "setInterval(() => {}, 1000)"]);
  console.info(`test host PID ${child.pid}`);
  await once(child, "spawn");
  const exited = once(child, "exit");
  child.kill("SIGTERM");
  await exited;
  expect(child.exitCode).toBeNull();
  expect(child.signalCode).toBe("SIGTERM");
  let browserClosed = false;
  async function failingCapture(): Promise<void> {
    try {
      try {
        throw new Error("capture failed");
      } finally {
        await stopMockHost(child);
      }
    } finally {
      browserClosed = true;
    }
  }
  await expect(failingCapture()).rejects.toThrow("capture failed");
  expect(browserClosed).toBe(true);
}, 3000);

test("manifest records the supplied viewport", async () => {
  const directory = await mkdtemp(join(tmpdir(), "uitk-index-"));
  try {
    await writeCaptureIndex(directory, [], "source-sha", { width: 960, height: 540 });
    const manifest = JSON.parse(await readFile(join(directory, "manifest.json"), "utf8"));
    expect(manifest.viewport).toBe("960x540");
  } finally {
    await rm(directory, { recursive: true });
  }
});
