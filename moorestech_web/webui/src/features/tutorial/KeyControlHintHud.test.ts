// 一致&非blocking時のみ描画
// Renders only when matching and non-blocking
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { TutorialPresentationData } from "@/bridge";

const host = vi.hoisted(() => ({
  presentation: null as TutorialPresentationData | null,
  uiState: null as { state: string } | null,
  blockingSkit: false,
}));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    useTopic: (topic: string) => (topic === actual.Topics.tutorialPresentation ? host.presentation : null),
    useTopicSelector: (topic: string, selector: (data: unknown) => unknown) =>
      selector(topic === actual.Topics.uiState ? host.uiState : null),
  };
});
vi.mock("@/shared/uiState", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/uiState")>();
  return { ...actual, useBlockingSkitActive: () => host.blockingSkit };
});
vi.mock("@/shared/i18n", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/i18n")>();
  return { ...actual, useI18n: () => ({ t: (key: string) => `T:${key}` }) };
});

import { KeyControlHintHud } from "./KeyControlHintHud";

const keyControl = (elementId: string, keyName: string, uiState: string) => ({
  kind: "keyControl" as const, elementId, tutorialGuid: "22222222-2222-4222-8222-222222222222", keyName, uiState,
});

function render() {
  let renderer!: ReturnType<typeof create>;
  act(() => { renderer = create(createElement(KeyControlHintHud)); });
  return renderer;
}

describe("KeyControlHintHud", () => {
  afterEach(() => { host.presentation = null; host.uiState = null; host.blockingSkit = false; });

  it("uiStateが一致するヒントだけを描く", () => {
    host.uiState = { state: "GameScreen" };
    host.presentation = { revision: 1, sessions: [{ tutorialSessionId: "s1", challengeId: "c1", elements: [
      keyControl("k1", "Tab", "GameScreen"), keyControl("k2", "R", "PlayerInventory"),
    ] }] };
    const renderer = render();
    const hints = renderer.root.findAllByProps({ "data-testid": "key-control-hint" });
    expect(hints.length).toBe(1);
    expect(renderer.root.findByType("kbd").children).toEqual(["Tab"]);
    expect(renderer.root.findByType("span").children).toEqual(["T:challengeTutorial.22222222-2222-4222-8222-222222222222.text"]);
  });

  it("一致するヒントが無ければHUD自体を描かない", () => {
    host.uiState = { state: "ResearchTree" };
    host.presentation = { revision: 1, sessions: [{ tutorialSessionId: "s1", challengeId: "c1", elements: [keyControl("k1", "Tab", "GameScreen")] }] };
    const renderer = render();
    expect(renderer.root.findAllByProps({ "data-testid": "key-control-hint-hud" }).length).toBe(0);
  });

  it("blockingスキット中は描かない", () => {
    host.uiState = { state: "GameScreen" };
    host.blockingSkit = true;
    host.presentation = { revision: 1, sessions: [{ tutorialSessionId: "s1", challengeId: "c1", elements: [keyControl("k1", "Tab", "GameScreen")] }] };
    const renderer = render();
    expect(renderer.root.findAllByProps({ "data-testid": "key-control-hint-hud" }).length).toBe(0);
  });

  // 2件同時一致→縦積み(ADR0022)
  // Two simultaneous matches stack vertically
  it("同時に一致するヒントが2件あれば2件とも描く", () => {
    host.uiState = { state: "GameScreen" };
    host.presentation = { revision: 1, sessions: [{ tutorialSessionId: "s1", challengeId: "c1", elements: [
      keyControl("k1", "Tab", "GameScreen"), keyControl("k2", "E", "GameScreen"),
    ] }] };
    const renderer = render();
    const hints = renderer.root.findAllByProps({ "data-testid": "key-control-hint" });
    expect(hints.length).toBe(2);
  });
});
