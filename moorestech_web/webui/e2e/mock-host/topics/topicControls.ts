import type { ServerResponse } from "node:http";
import { readFileSync } from "node:fs";
import { WebSocket } from "ws";
import { Topics } from "../../../src/bridge/transport/protocol";
import type { TopicPayloads } from "../../../src/bridge/transport/protocol";
import { L } from "../../../src/shared/i18n/generated/localizationKeys";
// JavaScriptのcodegen parserには型宣言がない
// The JavaScript codegen parser has no type declarations
// @ts-expect-error Importing the plain ESM parser is intentional
import { parseLocalizationCsv } from "../../../scripts/generate-localization-keys.mjs";
import { researchNodeAnchorId } from "../../../src/shared/tutorialAnchor/anchorIds";
import * as fx from "../fixtures";
import { state, topicSubscribers } from "../state";
import { clone, send, setTopicRevision } from "../wire";

const dictionaries = createDictionaries();

export function serveDictionary(url: string, response: ServerResponse): void {
  const locale = url.split("/api/i18n/")[1]?.split("?")[0] ?? "japanese";
  response.writeHead(200, { "Content-Type": "application/json" });
  response.end(JSON.stringify(dictionaries.get(locale) ?? {}));
}

const control = <T extends keyof TopicPayloads>(topic: T, data: TopicPayloads[T]) => ({ topic, data });

// spec共有のSSOT値
// SSOT value shared with the spec
export const TUTORIAL_RESEARCH_NODE_PADDING_PX = 8;
const controls = {
  placement: () => control(Topics.placementMode, {
    selectedTargetType: "raw", selectedName: "Assembler", height: 3, unavailableReason: "", wheelOwnedByTool: false,
  }),
  placementUnavailable: () => control(Topics.placementMode, {
    selectedTargetType: "raw", selectedName: "Assembler", height: 3, unavailableReason: "Blocked by terrain", wheelOwnedByTool: false,
  }),
  placementEmpty: () => control(Topics.placementMode, {
    selectedTargetType: "raw", selectedName: "", height: 0, unavailableReason: "", wheelOwnedByTool: false,
  }),
  // connectToolはGuidのみ配信し表示名解決はWeb辞書に任せる
  // connectTool ships only its GUID and leaves display-name resolution to the web dictionary
  placementConnectTool: () => control(Topics.placementMode, {
    selectedTargetType: "connectTool", selectedConnectToolGuid: fx.WIRE_CONNECT_TOOL_GUID, height: 3, unavailableReason: "", wheelOwnedByTool: true,
  }),
  // trainCarもGuidのみ配信し表示名解決はWeb辞書に任せる
  // Train cars also ship only their GUID and leave display-name resolution to the web dictionary
  placementTrainCar: () => control(Topics.placementMode, {
    selectedTargetType: "trainCar", selectedTrainCarGuid: fx.CARGO_TRAIN_CAR_GUID, height: 3, unavailableReason: "", wheelOwnedByTool: false,
  }),
  crosshairHidden: () => control(Topics.crosshair, { visible: false }),
  crosshairVisible: () => control(Topics.crosshair, { visible: true }),
  uiHidden: () => control(Topics.uiVisibility, { visible: false }),
  uiVisible: () => control(Topics.uiVisibility, { visible: true }),
  progressLabeled: () => control(Topics.progress, { visible: true, progress: 0.4, label: "Crafting" }),
  mining: (params: URLSearchParams) => control(Topics.progress, {
    visible: true,
    progress: Number(params.get("progress") ?? "0.65"),
  }),
  miningHidden: () => control(Topics.progress, { visible: false, progress: 0 }),
  tooltip: () => control(Topics.tooltip, {
    visible: true,
    textKey: L.ui.tooltip.worldTarget,
    textParams: [],
    fontSize: 18,
  }),
  tooltipHidden: () => control(Topics.tooltip, {
    visible: false,
    textKey: "",
    textParams: [],
    fontSize: 14,
  }),
  pauseConnected: () => control(Topics.pauseMenu, { disconnected: false }),
  pauseDisconnected: () => control(Topics.pauseMenu, { disconnected: true }),
  japanese: () => control(Topics.localization, { locale: "japanese", revision: 1 }),
  english: () => control(Topics.localization, { locale: "english", revision: 1 }),
  challengeActive: () => control(Topics.challengeCurrent, clone(fx.challengeCurrent)),
  challengeJapanese: () => control(Topics.challengeCurrent, clone(fx.challengeJapanese)),
  challengeMultiple: () => control(Topics.challengeCurrent, clone(fx.challengeMultiple)),
  challengeLong: () => control(Topics.challengeCurrent, clone(fx.challengeLong)),
  challengeMultipleLong: () => control(Topics.challengeCurrent, clone(fx.challengeMultipleLong)),
  challengeCompleted: () => control(Topics.challengeCurrent, { challenges: [], completedChallengeGuid: "82000000-0000-4000-8000-000000000002" }),
  // サーバーはGuidを送りWebが辞書で名前解決するため、fixtureも研究Guidを渡す
  // The server sends GUIDs and the web resolves names via the dictionary, so the fixture passes a research GUID
  notificationAchievement: () => control(Topics.notification, { seq: 1, category: "achievement", messageId: "achievement.researchCompleted", messageParams: ["11111111-1111-4111-8111-111111111111"], itemId: 1 }),
  notificationItemUnlocked: () => control(Topics.notification, { seq: 2, category: "achievement", messageId: "achievement.unlockedItem", messageParams: [], itemId: 2 }),
  notificationDenied: () => control(Topics.notification, { seq: 3, category: "operationDenied", messageId: "denied.researchNotCompletable", messageParams: [], itemId: null }),
  // 後片付け用の空値リセット口
  // Reset hook for spec teardown
  notificationClear: () => control(Topics.notification, {}),
  tutorialOutline: () => control(Topics.tutorialPresentation, {
    revision: 1,
    sessions: [{
      tutorialSessionId: "tutorial-session-1", challengeId: "tutorial-challenge-1",
      elements: [{
        kind: "outline" as const,
        elementId: "tutorial-highlight-1",
        anchorId: "game.crosshair",
        paddingPx: 8, blocksPointerInput: false,
      }],
    }],
  }),
  // パンでクリップ境界を跨ぐノード
  // A node that crosses the clip edge on pan
  tutorialResearchNode: () => control(Topics.tutorialPresentation, {
    revision: 1,
    sessions: [{
      tutorialSessionId: "tutorial-session-research", challengeId: "tutorial-challenge-research",
      elements: [{
        kind: "outline" as const,
        elementId: "tutorial-highlight-research",
        anchorId: researchNodeAnchorId(fx.researchableNodeGuid),
        paddingPx: TUTORIAL_RESEARCH_NODE_PADDING_PX, blocksPointerInput: false,
      }],
    }],
  }),
  tutorialEmpty: () => control(Topics.tutorialPresentation, { revision: 0, sessions: [] }),
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

function createDictionaries(): Map<string, Record<string, string>> {
  const csvUrl = new URL("../../../../../Localization/localization.csv", import.meta.url);
  const csv = parseLocalizationCsv(readFileSync(csvUrl, "utf8"));
  const result = new Map<string, Record<string, string>>();

  // Sourceと各言語を本番CSVの同じ行集合から組み立てる
  // Build Source and every locale from the same production CSV rows
  result.set("source", {
    ...Object.fromEntries(csv.rows.map((row: { key: string; source: string }) => [row.key, row.source])),
    ...fx.itemNameDictionaries.source,
    ...fx.blockNameDictionaries.source,
    ...fx.contentLocalizationDictionaries.source,
  });
  for (let languageIndex = 0; languageIndex < csv.languageCodes.length; languageIndex += 1) {
    const languageCode = csv.languageCodes[languageIndex];
    result.set(
      languageCode,
      {
        ...Object.fromEntries(csv.rows.map((row: { key: string; texts: string[] }) => [row.key, row.texts[languageIndex]])),
        ...fx.itemNameDictionaries[languageCode],
        ...fx.blockNameDictionaries[languageCode],
        ...fx.contentLocalizationDictionaries[languageCode],
      },
    );
  }
  return result;
}
