import { mkdir, rm } from "node:fs/promises";
import type { Server } from "node:http";
import { join } from "node:path";
import type { Browser } from "@playwright/test";
import type { WebSocketServer } from "ws";

export async function listen(server: Server, port: number): Promise<void> {
  // loopbackへ固定し、外部プロセスとの境界で起動失敗を呼び出し元へ返す
  // Bind to loopback and propagate startup failures at the external-process boundary
  await new Promise<void>((resolve, reject) => {
    server.once("error", reject);
    server.listen(port, "127.0.0.1", () => {
      server.off("error", reject);
      resolve();
    });
  });
}

export async function prepareOutputDirectory(
  outputDirectory: string,
  artifactNames: string[],
): Promise<void> {
  // 既知の直下成果物だけを除き、他ファイルや子ディレクトリを保護する
  // Remove only known root artifacts while preserving other files and child directories
  await mkdir(outputDirectory, { recursive: true });
  await Promise.all(artifactNames.map((name) =>
    rm(join(outputDirectory, name), { force: true })));
}

export async function closeCaptureResources(
  browser: Browser | undefined,
  wss: WebSocketServer | undefined,
  server: Server | undefined,
): Promise<void> {
  // どれかの終了失敗が他資源の終了を妨げないよう独立して閉じる
  // Close resources independently so one shutdown failure cannot block the others
  const closures: Promise<unknown>[] = [];
  if (browser !== undefined) closures.push(browser.close());
  if (wss !== undefined) {
    closures.push(new Promise<void>((resolve, reject) =>
      wss.close((error) => error ? reject(error) : resolve())));
  }
  if (server?.listening) {
    closures.push(new Promise<void>((resolve, reject) =>
      server.close((error) => error ? reject(error) : resolve())));
  }
  // 全終了を待ってから先頭の失敗を返す
  // Return the first failure after every close settles
  const results = await Promise.allSettled(closures);
  for (const result of results) {
    if (result.status === "rejected") throw result.reason;
  }
}
