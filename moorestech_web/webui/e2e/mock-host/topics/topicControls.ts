import type { ServerResponse } from "node:http";
import { WebSocket } from "ws";
import { Topics } from "../../../src/bridge/transport/protocol";
import type { TopicPayloads } from "../../../src/bridge/transport/protocol";
import * as fx from "../fixtures";
import { state, topicSubscribers } from "../state";
import { clone, send, setTopicRevision } from "../wire";

const dictionaries: Record<string, Record<string, string>> = {
  japanese: { "Pause Menu": "ポーズメニュー", "Save this game": "セーブする", CONTEXT_INSPECT: "調べる", TOOLTIP_WORLD: "世界の対象" },
  english: { "Pause Menu": "Pause Menu", "Save this game": "Save Game", CONTEXT_INSPECT: "Inspect", TOOLTIP_WORLD: "World Target" },
};

export function serveDictionary(url: string, response: ServerResponse): void {
  const locale = url.split("/api/i18n/")[1]?.split("?")[0] ?? "japanese";
  response.writeHead(200, { "Content-Type": "application/json" });
  response.end(JSON.stringify(dictionaries[locale] ?? {}));
}

const control = <T extends keyof TopicPayloads>(topic: T, data: TopicPayloads[T]) => ({ topic, data });
const controls = {
  placement: () => control(Topics.placementMode, { selectedName: "Assembler", height: 3, unavailableReason: "", energizedRangeVisible: true }),
  delete: () => control(Topics.deleteMode, { unavailableReason: "Protected area" }),
  crosshairHidden: () => control(Topics.crosshair, { visible: false }),
  crosshairVisible: () => control(Topics.crosshair, { visible: true }),
  uiHidden: () => control(Topics.uiVisibility, { visible: false }),
  uiVisible: () => control(Topics.uiVisibility, { visible: true }),
  mining: (params: URLSearchParams) => control(Topics.miningHud, { visible: true, targetName: params.get("text") ?? "Iron Ore", mining: true, progress: 0.65 }),
  miningHidden: () => control(Topics.miningHud, { visible: false, targetName: "", mining: false, progress: 0 }),
  tooltip: () => control(Topics.tooltip, { visible: true, textKey: "TOOLTIP_WORLD", fontSize: 18 }),
  tooltipHidden: () => control(Topics.tooltip, { visible: false, textKey: "", fontSize: 14 }),
  pauseConnected: () => control(Topics.pauseMenu, { disconnected: false }),
  pauseDisconnected: () => control(Topics.pauseMenu, { disconnected: true }),
  japanese: () => control(Topics.localization, { locale: "japanese" }),
  english: () => control(Topics.localization, { locale: "english" }),
  challengeActive: () => control(Topics.challengeCurrent, clone(fx.challengeCurrent)),
  challengeCompleted: () => control(Topics.challengeCurrent, { challenges: [], completedChallengeGuid: "ch-2" }),
};
export type TopicScenario = keyof typeof controls;

export function applyTopicControl(url: string, response: ServerResponse): void {
  const params = new URL(url, "http://x").searchParams;
  const scenario = params.get("scenario") ?? "";
  const factory = controls[scenario as TopicScenario];
  const selectedControl = factory?.(params);
  const controlValue = selectedControl;
  if (!controlValue) {
    response.statusCode = 400;
    response.end(JSON.stringify({ ok: false, error: "unknown_scenario" }));
    return;
  }
  state.topicOverrides.set(controlValue.topic, clone(controlValue.data));
  const revision = params.has("revision") ? Number(params.get("revision")) : undefined;
  if (revision !== undefined && params.get("setWireRevision") === "1") setTopicRevision(controlValue.topic, revision);
  for (const ws of topicSubscribers.get(controlValue.topic) ?? []) {
    if (ws.readyState !== WebSocket.OPEN) continue;
    send(ws, { op: params.get("snapshot") === "1" ? "snapshot" : "event", topic: controlValue.topic, revision, data: controlValue.data });
  }
  response.end(JSON.stringify({ ok: true }));
}
