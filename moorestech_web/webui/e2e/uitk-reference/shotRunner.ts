import { spawn, type ChildProcess } from "node:child_process";
import { expect, type Page } from "@playwright/test";
import { requestMockControl, revealBlockingSkitBody } from "../support/mockControl";
import { blockingSkitText } from "../mock-host/fixtures";
import type { ShotSetup, ShotStep } from "./referenceShots";

const HOST_START_TIMEOUT_MS = 15_000;
const FRAME_SETTLE_MS = 600;
const EXPECTATION_TIMEOUT_MS = 10_000;

export const referenceViewport = { width: 1280, height: 720 } as const;

// 撮影ごとに mock-host を別プロセスで起こし、前の撮影の状態を持ち越さない（外部プロセス境界）
// Start a fresh mock-host process per shot so no state leaks between shots (external process boundary)
export async function startMockHost(webuiRoot: string, port: number): Promise<ChildProcess> {
  const child = spawn(process.execPath, ["--import", "tsx", "e2e/mock-host/server.ts"], {
    cwd: webuiRoot, env: { ...process.env, MOCK_PORT: String(port), MOCK_DEMO: "1" }, stdio: ["ignore", "pipe", "inherit"],
  });
  console.log(`mock-host PID ${child.pid} on ${port}`);
  // 待受の合図か、先に終了か、15 秒の期限のどれかで決着させる
  // Settle on the listening line, an early exit, or a 15 second deadline
  await new Promise<void>((resolve, reject) => {
    const deadline = setTimeout(() => {
      child.kill();
      reject(new Error(`mock-host did not listen within ${HOST_START_TIMEOUT_MS}ms on ${port}`));
    }, HOST_START_TIMEOUT_MS);
    child.once("exit", (code) => {
      clearTimeout(deadline);
      reject(new Error(`mock-host exited before listening (code ${code})`));
    });
    child.stdout?.on("data", (chunk: Buffer) => {
      if (!chunk.toString().includes(`mock-host on ${port}`)) return;
      clearTimeout(deadline);
      resolve();
    });
  });
  return child;
}

export async function stopMockHost(child: ChildProcess): Promise<void> {
  if (child.exitCode !== null || child.signalCode !== null) return; // silent-ok: 終了済み / 呼出元が後片付けを続行
  const exited = new Promise<void>((resolve) => child.once("exit", () => resolve()));
  child.kill();
  await exited;
}

// 制御を当ててから開き、手順を流し、カーソルを隅へ退けて落ち着くのを待つ
// Apply controls, open the page, run the steps, park the cursor, and let the frame settle
export async function openShot(page: Page, baseUrl: string, setup: ShotSetup): Promise<void> {
  await prepareShot(page, baseUrl, setup);
  await finishShot(page, baseUrl, setup);
}

export async function prepareShot(page: Page, baseUrl: string, setup: ShotSetup): Promise<void> {
  for (const path of setup.controls) await requestMockControl(page, baseUrl, path);
  await page.goto(baseUrl);
  await page.evaluate("document.fonts.ready.then(() => undefined)");
  await page.getByTestId("app-stage").waitFor({ state: "attached" });
}

export async function finishShot(page: Page, baseUrl: string, setup: ShotSetup): Promise<void> {
  for (const step of setup.steps) await runStep(page, baseUrl, step);
  await page.mouse.move(2, 2);
  await page.waitForTimeout(FRAME_SETTLE_MS);
}

async function runStep(page: Page, baseUrl: string, step: ShotStep): Promise<void> {
  switch (step.kind) {
    case "control": return requestMockControl(page, baseUrl, step.path);
    case "click": return page.getByTestId(step.testId).first().click();
    case "rightClickFirstChild": return page.getByTestId(step.testId).locator("> div").first().click({ button: "right" });
    case "waitTestId": return page.getByTestId(step.testId).first().waitFor();
    case "revealSkit": return revealBlockingSkitBody(page, blockingSkitText.presentationState.body);
    case "expectVisible": {
      const target = page.getByTestId(step.testId);
      return expect((step.descendant === null ? target : target.locator(step.descendant)).first()).toBeVisible({ timeout: EXPECTATION_TIMEOUT_MS });
    }
    case "expectContent": {
      const targets = page.getByTestId(step.testId);
      await expect(targets).toHaveCount(step.texts.length, { timeout: EXPECTATION_TIMEOUT_MS });
      for (const [index, text] of step.texts.entries()) {
        await expect(targets.nth(index)).toBeVisible({ timeout: EXPECTATION_TIMEOUT_MS });
        await expect(targets.nth(index)).toContainText(text, { timeout: EXPECTATION_TIMEOUT_MS });
      }
      return;
    }
    case "wait": return page.waitForTimeout(step.ms);
  }
}
