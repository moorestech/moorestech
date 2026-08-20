import type { TutorialPresentationData } from "@/bridge";

export type TutorialSession = TutorialPresentationData["sessions"][number];
export type TutorialOverlayElement = TutorialSession["elements"][number];

// anchorを持たない要素（keyControl）を列挙前に除外する型ガード
// Type guard filtering out elements without an anchor (keyControl) before iteration
export type AnchoredTutorialElement = Exclude<TutorialOverlayElement, { kind: "keyControl" }>;

export function isAnchoredElement(element: TutorialOverlayElement): element is AnchoredTutorialElement {
  return element.kind !== "keyControl";
}

// sessionId+elementIdのreact key導出。TutorialOverlay/KeyControlHintHud共通
// Derives the react key from sessionId+elementId, shared by TutorialOverlay/KeyControlHintHud
export function tutorialElementKey(tutorialSessionId: string, elementId: string): string {
  return `${tutorialSessionId}:${elementId}`;
}
