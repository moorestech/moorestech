import type { Page } from "@playwright/test";
import type { TopicScenario } from "../mock-host/topics/topicControls";

// mock-host 制御エンドポイントの薄いラッパ。URL リテラルの散在を防ぐ
// Thin wrappers over the mock-host control endpoints; keeps URL literals in one place
export async function requestMockControl(page: Page, baseUrl: string, path: string): Promise<void> {
  // HTTP・mock応答を境界で検証する
  // Validate HTTP and mock responses at the boundary
  const response = await page.request.get(`${baseUrl}${path}`);
  const body = await response.json() as unknown;
  const accepted = typeof body === "object" && body !== null && "ok" in body && body.ok === true;
  if (!response.ok() || !accepted) {
    throw new Error(`mock control failed: ${path} (${response.status()}) ${JSON.stringify(body)}`);
  }
}

export function setBlock(page: Page, type: string) {
  return page.request.get(`/__block?type=${type}`);
}

export function setModal(page: Page, show: boolean) {
  return page.request.get(`/__modal?show=${show ? 1 : 0}`);
}

export function setUiState(page: Page, state: string, subState?: string) {
  const subStateQuery = subState ? `&subState=${encodeURIComponent(subState)}` : "";
  return page.request.get(`/__uistate?state=${state}${subStateQuery}`);
}

export function setTrainRiding(page: Page, riding: boolean, count: number, selected: number) {
  return page.request.get(`/__train-riding?riding=${riding ? 1 : 0}&count=${count}&selected=${selected}`);
}

export function resetResearch(page: Page) {
  return page.request.get("/__research");
}

export function injectActionError(page: Page, type: string, error: string) {
  return page.request.get(`/__action-error?type=${encodeURIComponent(type)}&error=${encodeURIComponent(error)}`);
}

export function clearActionError(page: Page) {
  return page.request.get("/__action-error");
}

export function setSnapshotDelay(page: Page, milliseconds: number, topic: string | null) {
  const topicQuery = topic === null ? "" : `&topic=${encodeURIComponent(topic)}`;
  return page.request.get(`/__snapshot-delay?ms=${milliseconds}${topicQuery}`);
}

export function disconnectWebSockets(page: Page, holdMilliseconds: number) {
  return page.request.get(`/__disconnect?holdMs=${holdMilliseconds}`);
}

export function setSkitStage(page: Page, stage: "none" | "background" | "text" | "choices") {
  return page.request.get(`/__skit?stage=${stage}`);
}

export async function setTopicScenario(page: Page, scenario: TopicScenario) {
  return requestMockControl(page, "", `/__topic-control?scenario=${encodeURIComponent(scenario)}`);
}

export async function setMiningProgress(page: Page, progress: number) {
  return requestMockControl(page, "", `/__topic-control?scenario=mining&progress=${progress}`);
}

export async function injectTopicSnapshot(page: Page, scenario: TopicScenario, revision: number, progress: number) {
  const path = `/__topic-control?scenario=${encodeURIComponent(scenario)}&snapshot=1&revision=${revision}&progress=${progress}`;
  return requestMockControl(page, "", path);
}

export async function setTopicScenarioRevision(page: Page, scenario: TopicScenario, revision: number, progress: number) {
  const path = `/__topic-control?scenario=${encodeURIComponent(scenario)}&revision=${revision}&setWireRevision=1&progress=${progress}`;
  return requestMockControl(page, "", path);
}

export async function setWoodItemGuid(page: Page, itemGuid: string) {
  return requestMockControl(page, "", `/__item-master?woodGuid=${encodeURIComponent(itemGuid)}`);
}
