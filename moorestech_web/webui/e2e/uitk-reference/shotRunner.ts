import { spawn, type ChildProcess } from "node:child_process";
import type { Page } from "@playwright/test";
import { requestMockControl, revealBlockingSkitBody } from "../support/mockControl";
import { blockingSkitText } from "../mock-host/fixtures";
import type { ShotSetup, ShotStep } from "./referenceShots";

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
      reject(new Error(`mock-host did not listen within 15s on ${port}`));
    }, 15_000);
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
  for (const path of setup.controls) await requestMockControl(page, baseUrl, path);
  await page.goto(baseUrl);
  await page.evaluate("document.fonts.ready.then(() => undefined)");
  await page.getByTestId("app-stage").waitFor({ state: "attached" });
  for (const step of setup.steps) await runStep(page, baseUrl, step);
  await page.mouse.move(2, 2);
  await page.waitForTimeout(600);
}

async function runStep(page: Page, baseUrl: string, step: ShotStep): Promise<void> {
  switch (step.kind) {
    case "control": return requestMockControl(page, baseUrl, step.path);
    case "click": return page.getByTestId(step.testId).first().click();
    case "rightClickFirstChild": return page.getByTestId(step.testId).locator("> div").first().click({ button: "right" });
    case "waitTestId": return page.getByTestId(step.testId).first().waitFor();
    case "revealSkit": return revealBlockingSkitBody(page, blockingSkitText.presentationState.body);
    case "wait": return page.waitForTimeout(step.ms);
  }
}
