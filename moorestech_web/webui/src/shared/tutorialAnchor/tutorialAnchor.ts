import type { DynamicTutorialAnchorId, StaticTutorialAnchorId } from "./anchorIds";

export type TutorialAnchorId = StaticTutorialAnchorId | DynamicTutorialAnchorId;
export type AnchorId = TutorialAnchorId;

export type TutorialAnchorAttributes = Readonly<{
  "data-tutorial-anchor": string;
}>;

// 1要素が複数名乗れるよう空白区切りにする
// A whitespace-separated list lets one element declare several anchors
export function tutorialAnchor(first: TutorialAnchorId, ...rest: TutorialAnchorId[]): TutorialAnchorAttributes {
  return { "data-tutorial-anchor": [first, ...rest].join(" ") };
}

// resolveAnchorとregistry共通のセレクタ
// Token-match selector shared by resolveAnchor and the registry
export function tutorialAnchorSelector(anchorId: string): string {
  const escaped = globalThis.CSS?.escape ? globalThis.CSS.escape(anchorId) : anchorId.replaceAll('"', '\\"');
  return `[data-tutorial-anchor~="${escaped}"]`;
}
