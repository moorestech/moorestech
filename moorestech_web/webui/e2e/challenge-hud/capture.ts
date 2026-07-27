import { writeFile } from "node:fs/promises";
import type { Server } from "node:http";
import { join } from "node:path";
import { chromium, type Browser } from "@playwright/test";
import { WebSocketServer } from "ws";
import { captureCases, captureImageNames, captureViewport } from "./cases";
import { capturePage } from "./pageCapture";
import { closeCaptureResources, listen, prepareOutputDirectory } from "./serverLifecycle";

const port = Number(process.env.CHALLENGE_CAPTURE_PORT ?? 5377);
const baseUrl = `http://127.0.0.1:${port}`;
const outputDirectory = process.env.CHALLENGE_CAPTURE_OUT ?? "/tmp/challenge-hud-visual-qa";
const metricsFile = "metrics.json";
const manifestFile = "manifest.json";

async function main(): Promise<void> {
  // DEMO背景を有効化してからmock-hostを読み込む
  // Enable the demo world before loading the mock host
  process.env.MOCK_DEMO = "1";
  const { createMockHttpServer } = await import("../mock-host/httpHandler");
  const { attachWsHandlers } = await import("../mock-host/wsHandler");
  let server: Server | undefined;
  let wss: WebSocketServer | undefined;
  let browser: Browser | undefined;

  try {
    // HTTP待受成功後だけWebSocketを接続し、起動失敗時のerror漏れを防ぐ
    // Attach WebSocket only after HTTP listens to prevent leaked startup errors
    server = createMockHttpServer();
    await listen(server, port);
    wss = new WebSocketServer({ server, path: "/ws" });
    attachWsHandlers(wss);
    await prepareOutputDirectory(outputDirectory, [...captureImageNames, metricsFile, manifestFile]);
    browser = await chromium.launch();
    const page = await browser.newPage({ viewport: captureViewport });
    const measurements: Record<string, unknown> = {};

    // 全ケースを固定順で撮影し、ケース名と計測キーを一致させる
    // Capture every case in fixed order and align case names with measurement keys
    for (const captureCase of captureCases) {
      measurements[captureCase.name] = await capturePage(page, baseUrl, outputDirectory, captureCase);
    }
    await writeFile(join(outputDirectory, metricsFile), JSON.stringify(measurements, null, 2));
    await writeFile(join(outputDirectory, manifestFile), JSON.stringify({
      viewport: captureViewport,
      images: captureImageNames,
      metrics: metricsFile,
    }, null, 2));
  } finally {
    // 正常・失敗経路の双方で生成済み資源を独立して閉じる
    // Independently close created resources on both success and failure paths
    await closeCaptureResources(browser, wss, server);
  }
}

// 外部境界の失敗を終了コードへ変換する
// Convert external-boundary failures into an exit code
void main().catch((error: unknown) => {
  console.error(error);
  process.exitCode = 1;
});
