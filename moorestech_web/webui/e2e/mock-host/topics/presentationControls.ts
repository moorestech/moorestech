import type { ServerResponse } from "node:http";
import { Topics } from "../../../src/bridge/transport/protocol";
import * as fx from "../fixtures";
import { send, clone } from "../wire";
import { state, subscribersOf } from "../state";

// presentation系（worldpin/gamestate/skit）のテスト用HTTP制御。httpHandlerの200行制限対策で分離
// Test-only HTTP controls for presentation topics (worldpin/gamestate/skit); split out for the 200-line rule
export function applyPresentationControl(url: string, res: ServerResponse): boolean {
  // テスト用: ワールドピンの射影値を差し替えて購読者へ event push（clear=1で全消去）
  // Test-only: replace the projected world pins and push an event to subscribers (clear=1 empties them)
  if (url.startsWith("/__worldpin")) {
    const params = new URL(url, "http://x").searchParams;
    const pins = params.get("clear") === "1" ? [] : [{
      pinId: params.get("id") ?? "map-object-pin",
      text: params.get("text") ?? "Pin",
      screenX: Number(params.get("x") ?? 0.5),
      screenY: Number(params.get("y") ?? 0.5),
      onScreen: params.get("on") !== "0",
      directionX: Number(params.get("dx") ?? 0),
      directionY: Number(params.get("dy") ?? 0),
    }];
    state.worldPins = { revision: state.worldPins.revision + 1, pins };
    for (const ws of subscribersOf(Topics.worldPins)) send(ws, { op: "event", topic: Topics.worldPins, data: state.worldPins });
    res.setHeader("content-type", "application/json");
    res.end(JSON.stringify({ ok: true }));
    return true;
  }
  if (url.startsWith("/__gamestate")) {
    const value = new URL(url, "http://x").searchParams.get("state") ?? "InGame";
    state.gameState = { state: value as "InGame" | "Skit" | "CutScene" };
    for (const ws of subscribersOf(Topics.gameState)) send(ws, { op: "event", topic: Topics.gameState, data: state.gameState });
    res.end(JSON.stringify({ ok: true }));
    return true;
  }
  if (url.startsWith("/__skit")) {
    const params = new URL(url, "http://x").searchParams;
    // S1のshow契約を維持しつつ、S2/S3の段階fixtureを選択する
    // Preserve the S1 show contract while selecting S2/S3 staged fixtures
    const stage = params.get("stage") ?? (params.get("show") === "1" ? "background" : "none");
    state.skitPresentation = stage === "text" ? clone(fx.blockingSkitText)
      : stage === "choices" ? clone(fx.blockingSkitChoices)
      : stage === "background" ? {
      ...clone(fx.skitPresentation), sessionId: "bg-1", sceneRevision: 1,
      presentationState: { ...clone(fx.skitPresentation.presentationState), mode: "background",
        speakerName: "Moore", body: "Background message", textAreaVisible: true,
        textReveal: { mode: "instant", intervalMs: 0 } },
    } : clone(fx.skitPresentation);
    for (const ws of subscribersOf(Topics.skitPresentation)) send(ws, { op: "event", topic: Topics.skitPresentation, data: state.skitPresentation });
    res.end(JSON.stringify({ ok: true }));
    return true;
  }
  return false;
}
